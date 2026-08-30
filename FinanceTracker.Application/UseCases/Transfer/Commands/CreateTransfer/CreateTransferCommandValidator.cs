using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Application.Configurations.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.UseCases.Transfer.Commands.CreateTransfer;

public sealed class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
	public CreateTransferCommandValidator(IOptionsMonitor<MoneyLimitsOptions> moneyLimits)
	{
		RuleFor(command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The transfer amount must be greater than zero.")
			.LessThanOrEqualTo(valueToCompare: moneyLimits.CurrentValue.MaxAmount)
			.WithMessage(errorMessage: $"The transfer amount cannot exceed {moneyLimits.CurrentValue.MaxAmount:N2}.")
			.MustFitAmountScale();

		RuleFor(command => command.FromAccountId)
			.NotEmpty().WithMessage(errorMessage: "The source account cannot be empty.");

		RuleFor(command => command.ToAccountId)
			.NotEmpty().WithMessage(errorMessage: "The destination account cannot be empty.")
			.NotEqual(expression: c => c.FromAccountId)
			.WithMessage(errorMessage: "Source and destination accounts must be different.");

		RuleFor(command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(command => command.Description)
			.MaximumLength(maximumLength: 255)
			.WithMessage(errorMessage: "The description cannot exceed 255 characters.")
			.When(predicate: command => command.Description is not null);
	}
}
