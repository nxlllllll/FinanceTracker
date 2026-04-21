using FluentValidation;

namespace FinanceTracker.Application.Users.Commands.ChangeUserBaseCurrency;

public sealed class ChangeUserBaseCurrencyCommandValidator : AbstractValidator<ChangeUserBaseCurrencyCommand>
{
	public ChangeUserBaseCurrencyCommandValidator()
	{
		RuleFor(expression: command => command.NewBaseCurrency)
			.NotEmpty().WithMessage(errorMessage: "The base currency code cannot be empty.")
			.Length(exactLength: 3).WithMessage(errorMessage: "The base currency code must be 3 characters.");
	}
}