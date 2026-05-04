using FinanceTracker.Core.Results;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace FinanceTracker.Application.Behaviours.Validation;

public sealed class ValidationBehavior<TRequest, TResponse>(
	IEnumerable<IValidator<TRequest>> validators
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
 
		if (errors.Count != 0)
			return TResponse.CreateFailure(error: new FinanceTracker.Core.Exceptions.ValidationException(errors: errors));
 
		return await next(t: cancellationToken);
	}
}