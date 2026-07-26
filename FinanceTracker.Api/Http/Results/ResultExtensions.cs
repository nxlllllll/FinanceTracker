using System.Reflection;
using System.Text.Json;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Http.Results;

/// <summary>Maps <see cref="Result{TValue,TError}"/> failures onto HTTP responses.</summary>
public static class ResultExtensions
{
	private static string ResolveErrorCode(AppException error)
		=> error.GetType().GetCustomAttribute<ErrorCodeAttribute>()?.Code ?? error.GetType().Name;

	public static IHttpResult ToProblem(this AppException error)
	{
		if (error is ValidationException validation)
		{
			return Microsoft.AspNetCore.Http.Results.ValidationProblem(
				errors: validation.Errors.ToDictionary(keySelector: kv => kv.Key, elementSelector: kv => kv.Value),
				detail: validation.Message,
				extensions: new Dictionary<string, object?> { ["code"] = ResolveErrorCode(error: error) }
			);
		}

		if (error is RateLimitExceededException rateLimitExceeded)
		{
			return new ResultWithHeader(
				inner: Microsoft.AspNetCore.Http.Results.Problem(
					detail: error.Message,
					statusCode: StatusCodes.Status429TooManyRequests,
					title: error.GetType().Name,
					extensions: new Dictionary<string, object?> { ["code"] = ResolveErrorCode(error: error) }
				),
				headerName: "Retry-After",
				headerValue: rateLimitExceeded.RetryAfterSeconds.ToString()
			);
		}

		int statusCode = error switch
		{
			EmptyIdempotencyKeyException => StatusCodes.Status400BadRequest,
			InvalidCredentialsException or InvalidTokenException => StatusCodes.Status401Unauthorized,
			NotFoundException => StatusCodes.Status404NotFound,
			ConcurrencyConflictException or UniqueConstraintException => StatusCodes.Status409Conflict,
			IdempotencyTimeoutException or IdempotencyAbandonedException => StatusCodes.Status409Conflict,
			SelfPermissionModificationException => StatusCodes.Status403Forbidden,
			PreconditionFailedException => StatusCodes.Status412PreconditionFailed,
			DomainException => StatusCodes.Status422UnprocessableEntity,
			_ => StatusCodes.Status500InternalServerError
		};

		return Microsoft.AspNetCore.Http.Results.Problem(
			detail: error.Message,
			statusCode: statusCode,
			title: error.GetType().Name,
			extensions: new Dictionary<string, object?> { ["code"] = ResolveErrorCode(error: error) }
		);
	}

	public static IHttpResult ToHttpResult<TReadModel, TResponse>(
		this Result<TReadModel, AppException> result,
		Func<TReadModel, string>? etag = null,
		Action<TReadModel>? onSuccess = null,
		Action<AppException>? onError = null
	) where TResponse : IResponseOf<TReadModel, TResponse>
	{
		if (result.IsFailure)
		{
			onError?.Invoke(obj: result.Error!);
			return result.Error!.ToProblem();
		}

		onSuccess?.Invoke(obj: result.Value!);
		IHttpResult ok = Microsoft.AspNetCore.Http.Results.Ok(value: TResponse.FromReadModel(readModel: result.Value!));

		if (etag is null)
			return ok;

		return new ResultWithHeader(
			inner: ok,
			headerName: "ETag",
			headerValue: etag(arg: result.Value!)
		);
	}

	public static IHttpResult ToHttpResult<TReadModel, TResponse>(
		this Result<IReadOnlyList<TReadModel>, AppException> result,
		Action<IReadOnlyList<TReadModel>>? onSuccess = null,
		Action<AppException>? onError = null
	) where TResponse : IResponseOf<TReadModel, TResponse>
	{
		if (result.IsFailure)
		{
			onError?.Invoke(obj: result.Error!);
			return result.Error!.ToProblem();
		}

		onSuccess?.Invoke(obj: result.Value!);
		return Microsoft.AspNetCore.Http.Results.Ok(value: result.Value!.Select(selector: TResponse.FromReadModel).ToList());
	}


	public static IHttpResult ToCreatedResult(
		this Result<Guid, AppException> result,
		Func<Guid, string> locationFactory
	) => result.IsSuccess ? Microsoft.AspNetCore.Http.Results.Created(uri: locationFactory(result.Value), value: new CreatedIdResponse(Id: result.Value)) : result.Error!.ToProblem();

	public static IHttpResult ToNoContentResult<TValue>(this Result<TValue, AppException> result)
		=> result.IsSuccess ? Microsoft.AspNetCore.Http.Results.NoContent() : result.Error!.ToProblem();

	public static IHttpResult ToValidationProblem(this DomainException error, string fieldName)
	{
		string camelCaseField = JsonNamingPolicy.CamelCase.ConvertName(name: fieldName);
		return new ValidationException(errors: new Dictionary<string, string[]>
		{
			[camelCaseField] = [error.Message]
		}).ToProblem();
	}
}
