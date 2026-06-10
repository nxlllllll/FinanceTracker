using FinanceTracker.Core.Results;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Validation;

/// <summary>
/// MediatR pipeline behaviour that runs FluentValidation validators for write commands.
/// Returns <c>Result.Failure</c> with a <c>ValidationException</c> if any validator fails,
/// without invoking the handler. No-op when no validators are registered for the request.
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(
	IEnumerable<IValidator<TRequest>> validators,
	ILogger<ValidationBehaviour<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
	where TResponse : IResult<TResponse, FinanceTracker.Core.Exceptions.ValidationException>
{
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		if (!validators.Any())
			return await next(t: cancellationToken);
 
		ValidationContext<TRequest> context = new ValidationContext<TRequest>(instanceToValidate: request);
 
		ValidationResult[] results = await Task.WhenAll(tasks: validators.Select(
			selector: validator => validator.ValidateAsync(context: context, cancellation: cancellationToken)
		));
 
		List<string> errors = results
			.SelectMany(selector: result => result.Errors)
			.Where(predicate: error => error is not null)
			.Select(selector: error => error.ErrorMessage)
			.ToList();

		if (errors.Count == 0)
			return await next(t: cancellationToken);
		
		logger.ZLogWarning(message: $"Validation failed for {request.GetType().Name}: {errors.Count} error(s).");
		return TResponse.CreateFailure(error: new FinanceTracker.Core.Exceptions.ValidationException(errors: errors));
	}
}
