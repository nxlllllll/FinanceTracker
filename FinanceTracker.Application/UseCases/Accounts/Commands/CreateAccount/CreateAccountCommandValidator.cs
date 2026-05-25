using FinanceTracker.Core.Repositories.Currency;
using FluentValidation;

namespace FinanceTracker.Application.UseCases.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
	public CreateAccountCommandValidator(ICurrencyReadRepository currencyReadRepository)
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.InitialBalance)
			.GreaterThanOrEqualTo(valueToCompare: 0).WithMessage(errorMessage: "The initial balance cannot be negative.");

		RuleFor(expression: command => command.Type)
			.IsInEnum().WithMessage(errorMessage: "The account type is invalid.");

		RuleFor(expression: command => command.Currency)
			.MustAsync(predicate: async (currency, ct) => await currencyReadRepository.ExistsAsync(code: currency.Value, ct: ct))
			.WithMessage(errorMessage: "The currency code does not exist.");
	}
}
