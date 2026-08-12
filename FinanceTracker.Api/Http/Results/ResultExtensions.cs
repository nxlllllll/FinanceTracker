using System.Reflection;
using System.Text.Json;
using FinanceTracker.Api.Endpoints.Shared;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Auth;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Permission;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Data;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Api.Http.Results;

/// <summary>Maps <see cref="Result{TValue,TError}"/> failures onto HTTP responses.</summary>
public static class ResultExtensions
{
	public static IHttpResult ToProblem(this AppException error) => error switch
	{
		ValidationException validation => ValidationProblem(error: validation),
		RateLimitExceededException rateLimit => RetryableProblem(
			error: rateLimit,
			statusCode: StatusCodes.Status429TooManyRequests,
			retryAfterSeconds: rateLimit.RetryAfterSeconds
		),
		TransientException transient => RetryableProblem(
			error: transient,
			statusCode: StatusCodes.Status503ServiceUnavailable,
			retryAfterSeconds: transient.RetryAfterSeconds
		),
		_ => Problem(error: error, statusCode: StatusCodeFor(error: error))
	};

	private static int StatusCodeFor(AppException error) => error switch
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

	private static IHttpResult ValidationProblem(ValidationException error) => Microsoft.AspNetCore.Http.Results.ValidationProblem(
		errors: error.Errors.ToDictionary(keySelector: kv => kv.Key, elementSelector: kv => kv.Value),
		detail: error.Message,
		extensions: Extensions(error: error)
	);

	private static IHttpResult RetryableProblem(
		AppException error,
		int statusCode,
		int retryAfterSeconds
	) => new ResultWithHeader(
		inner: Problem(error: error, statusCode: statusCode),
		headerName: "Retry-After",
		headerValue: retryAfterSeconds.ToString()
	);

	private static IHttpResult Problem(
		AppException error,
		int statusCode
	) => Microsoft.AspNetCore.Http.Results.Problem(
		detail: error.Message,
		statusCode: statusCode,
		extensions: Extensions(error: error)
	);

	private static Dictionary<string, object?> Extensions(AppException error)
		=> new Dictionary<string, object?> { ["code"] = ResolveErrorCode(error: error) };

	private static string ResolveErrorCode(AppException error)
		=> error.GetType().GetCustomAttribute<ErrorCodeAttribute>()?.Code ?? error.GetType().Name;

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
		Func<Guid, string> locationFactory)
	{
		if (result.IsFailure)
			return result.Error!.ToProblem();

		return Microsoft.AspNetCore.Http.Results.Created(
			uri: locationFactory(result.Value),
			value: new CreatedIdResponse(Id: result.Value)
		);
	}

	public static IHttpResult ToCreatedAtRoute(
		this Result<Guid, AppException> result,
		LinkGenerator linkGenerator,
		HttpContext httpContext,
		string routeName,
		Func<Guid, object> routeValues)
	{
		if (result.IsFailure)
			return result.Error!.ToProblem();

		string? location = linkGenerator.GetPathByName(
			httpContext: httpContext,
			endpointName: routeName,
			values: routeValues(result.Value)
		);

		if (location is null)
			throw new InvalidOperationException(message: $"No route named '{routeName}' is mapped, so the Location header for a newly created resource cannot be built.");

		return Microsoft.AspNetCore.Http.Results.Created(
			uri: location,
			value: new CreatedIdResponse(Id: result.Value)
		);
	}

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
