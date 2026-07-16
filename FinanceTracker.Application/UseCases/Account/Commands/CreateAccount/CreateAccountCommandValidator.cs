using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Core.Repositories.Currency;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
	public CreateAccountCommandValidator(
		ICurrencyReadRepository currencyReadRepository,
		IOptionsMonitor<MoneyLimitsOptions> moneyLimits)
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(expression: command => command.InitialBalance)
			.GreaterThanOrEqualTo(valueToCompare: 0).WithMessage(errorMessage: "The initial balance cannot be negative.")
			.LessThanOrEqualTo(valueToCompare: moneyLimits.CurrentValue.MaxAmount)
			.WithMessage(errorMessage: $"The initial balance cannot exceed {moneyLimits.CurrentValue.MaxAmount:N2}.");

		RuleFor(expression: command => command.Type)
			.IsInEnum().WithMessage(errorMessage: "The account type is invalid.");

		RuleFor(expression: command => command.Currency)
			.MustAsync(predicate: async (currency, ct) => await currencyReadRepository.ExistsAsync(code: currency.Value, ct: ct))
			.WithMessage(errorMessage: "The currency code does not exist.");
	}
}
