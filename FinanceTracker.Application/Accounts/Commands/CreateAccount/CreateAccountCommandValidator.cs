using FluentValidation;

namespace FinanceTracker.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
	public CreateAccountCommandValidator()
	{
		RuleFor(expression: command => command.Name)
			.NotEmpty().WithMessage("The name cannot be empty.");

		RuleFor(expression: command => command.InitialBalance)
			.GreaterThanOrEqualTo(valueToCompare: 0).WithMessage("The initial balance cannot be negative.");
	}
}