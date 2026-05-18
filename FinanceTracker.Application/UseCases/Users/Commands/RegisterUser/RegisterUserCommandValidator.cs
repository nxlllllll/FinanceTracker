using FinanceTracker.Core.Repositories.Currency;
using FluentValidation;

namespace FinanceTracker.Application.UseCases.Users.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
	public RegisterUserCommandValidator(ICurrencyReadRepository currencyReadRepository)
	{
		RuleFor(expression: command => command.Email)
			.NotEmpty().WithMessage(errorMessage: "The email cannot be empty.")
			.EmailAddress().WithMessage(errorMessage: "The email is invalid.");

		RuleFor(expression: command => command.Password)
			.NotEmpty().WithMessage(errorMessage: "The password cannot be empty.")
			.MinimumLength(minimumLength: 8).WithMessage(errorMessage: "The password must be at least 8 characters.");
	
		RuleFor(expression: command => command.BaseCurrencyCode)
			.MustAsync(predicate: async (currency, ct) => await currencyReadRepository.ExistsAsync(code: currency.Value, ct: ct))
			.WithMessage(errorMessage: "The currency code does not exist.");
	}
}