using FluentValidation;

namespace FinanceTracker.Application.UseCases.Users.Commands.ChangeUserBaseCurrency;

public sealed class ChangeUserBaseCurrencyCommandValidator : AbstractValidator<ChangeUserBaseCurrencyCommand>
{
	public ChangeUserBaseCurrencyCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");
		
		RuleFor(expression: command => command.NewBaseCurrency)
			.NotEmpty().WithMessage(errorMessage: "The base currency code cannot be empty.")
			.Length(exactLength: 3).WithMessage(errorMessage: "The base currency code must be 3 characters.");
	}
}