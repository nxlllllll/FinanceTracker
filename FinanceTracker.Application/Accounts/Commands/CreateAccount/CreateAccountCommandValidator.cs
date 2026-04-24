using FluentValidation;

namespace FinanceTracker.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
	public CreateAccountCommandValidator()
	{
		RuleFor(expression: command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");
		
		RuleFor(expression: command => command.Name)
			.NotEmpty().WithMessage(errorMessage: "The name cannot be empty.");

		RuleFor(expression: command => command.InitialBalance)
			.GreaterThanOrEqualTo(valueToCompare: 0)
			.WithMessage(errorMessage: "The initial balance cannot be negative.");
		
		RuleFor(expression: command => command.Type)
			.IsInEnum().WithMessage(errorMessage: "The account type is invalid.");

		RuleFor(expression: command => command.Currency)
			.Length(exactLength: 3).WithMessage(errorMessage: "The currency must be 3 characters.");
	}
}