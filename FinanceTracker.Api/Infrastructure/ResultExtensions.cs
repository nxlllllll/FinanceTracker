using System.Net;
using FinanceTracker.Api.Contracts.Abstractions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace FinanceTracker.Api.Infrastructure;

/// <summary>Maps <see cref="Result{TValue,TError}"/> failures onto HTTP responses.</summary>
public static class ResultExtensions
{
	public static IHttpResult ToProblem(this AppException error)
	{
		if (error is ValidationException validation)
		{
			return Results.Problem(
				detail: validation.Message,
				statusCode: StatusCodes.Status400BadRequest,
				title: nameof(ValidationException),
				extensions: new Dictionary<string, object?> { ["errors"] = validation.Errors }
			);
		}

		int statusCode = error switch
		{
			EmptyIdempotencyKeyException => StatusCodes.Status400BadRequest,
			InvalidCredentialsException or InvalidTokenException => StatusCodes.Status401Unauthorized,
			NotFoundException => StatusCodes.Status404NotFound,
			ConcurrencyConflictException or UniqueConstraintException => StatusCodes.Status409Conflict,
			IdempotencyTimeoutException or IdempotencyAbandonedException => StatusCodes.Status409Conflict,
			RateLimitExceededException => StatusCodes.Status429TooManyRequests,
			SelfPermissionModificationException => StatusCodes.Status403Forbidden,
			DomainException => StatusCodes.Status422UnprocessableEntity,
			_ => StatusCodes.Status500InternalServerError
		};

		return Results.Problem(
			detail: error.Message,
			statusCode: statusCode,
			title: error.GetType().Name
		);
	}

	public static IHttpResult ToHttpResult<TReadModel, TResponse>(
		this Result<TReadModel, AppException> result,
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
		return Results.Ok(value: TResponse.FromReadModel(readModel: result.Value!));
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
		return Results.Ok(value: result.Value!.Select(selector: TResponse.FromReadModel).ToList());
	}


	public static IHttpResult ToCreatedResult(
		this Result<Guid, AppException> result,
		Func<Guid, string> locationFactory
	) => result.IsSuccess ? Results.Created(uri: locationFactory(result.Value), value: new CreatedIdResponse(Id: result.Value)) : result.Error!.ToProblem();

	public static IHttpResult ToNoContentResult<TValue>(this Result<TValue, AppException> result)
		=> result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();

	public static IHttpResult ToValidationProblem(this DomainException error)
		=> new ValidationException(errors: [error.Message]).ToProblem();
}
