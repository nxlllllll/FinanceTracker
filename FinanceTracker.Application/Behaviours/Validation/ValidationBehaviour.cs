using System.Text.Json;
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
		IValidator<TRequest>[] validatorsArray = validators as IValidator<TRequest>[] ?? [.. validators];
		if (validatorsArray.Length == 0)
			return await next(t: cancellationToken);

		ValidationContext<TRequest> context = new ValidationContext<TRequest>(instanceToValidate: request);

		List<ValidationFailure> failures = new List<ValidationFailure>(capacity: validatorsArray.Length);
		foreach (IValidator<TRequest> validator in validatorsArray)
		{
			ValidationResult result = await validator.ValidateAsync(context: context, cancellation: cancellationToken);
			if (result.Errors.Count > 0)
				failures.AddRange(collection: result.Errors.Where(predicate: e => e is not null));
		}

		if (failures.Count == 0)
			return await next(t: cancellationToken);

		Dictionary<string, string[]> errors = failures.GroupBy(keySelector: failure => JsonNamingPolicy.CamelCase.ConvertName(name: failure.PropertyName)).ToDictionary(
			keySelector: group => group.Key,
			elementSelector: group => group.Select(selector: failure => failure.ErrorMessage).ToArray()
		);

		string commandType = request.GetType().Name;
		int failureCount = failures.Count;
		int fieldCount = errors.Count;

		logger.ZLogWarning(message: $"Validation failed for {commandType}: {failureCount} error(s) across {fieldCount} field(s).");
		return TResponse.CreateFailure(error: new FinanceTracker.Core.Exceptions.ValidationException(errors: errors));
	}
}
