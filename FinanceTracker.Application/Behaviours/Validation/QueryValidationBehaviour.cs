using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Validation;

/// <summary>
/// MediatR pipeline behaviour that runs FluentValidation validators for queries.
/// Unlike <c>ValidationBehavior</c>, queries return plain values (not <c>Result</c>),
/// so validation failure throws a <c>ValidationException</c> directly.
/// No-op when no validators are registered for the request.
/// </summary>
public sealed class QueryValidationBehaviour<TRequest, TResponse>(
	IEnumerable<IValidator<TRequest>> validators,
	ILogger<QueryValidationBehaviour<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
	where TResponse : notnull
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

		List<ValidationFailure> errors = results
			.SelectMany(selector: result => result.Errors)
			.Where(predicate: error => error is not null)
			.ToList();

		if (errors.Count == 0)
			return await next(t: cancellationToken);
		
		logger.ZLogWarning(message: $"{request.GetType().Name} entity have errors: {errors.Count}");
		throw new ValidationException(errors: errors);
	}
}
