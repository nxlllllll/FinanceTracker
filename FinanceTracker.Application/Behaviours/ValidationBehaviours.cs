using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace FinanceTracker.Application.Behaviours;

public sealed class ValidationBehaviours<TRequest, TResponse>(
	IEnumerable<IValidator<TRequest>> validators
) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
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

		List<ValidationFailure> failures = results.SelectMany(selector: result => result.Errors)
												.Where(predicate: error => error is not null).ToList();

		if (failures.Count != 0)
			throw new ValidationException(errors: failures);

		return await next(t: cancellationToken);
	}
}