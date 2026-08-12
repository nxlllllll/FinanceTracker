using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Core.Services.DateProvider;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.UseCases.Transfer.Commands.CreateTransfer;

public sealed class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
	public CreateTransferCommandValidator(
		IDateProvider dateProvider,
		IOptionsMonitor<MoneyLimitsOptions> moneyLimits,
		IOptionsMonitor<BackdatingOptions> backdating)
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

		RuleFor(command => command.OccurredAt)
			.NotEmpty().WithMessage(errorMessage: "The transfer date cannot be empty.")
			.Must(predicate: date => date <= dateProvider.UtcNow)
			.WithMessage(errorMessage: "The transfer date cannot be in the future.")
			.Must(predicate: date => date >= dateProvider.UtcNow.AddMonths(months: -backdating.CurrentValue.MaxBackdatingMonths))
			.WithMessage(errorMessage: $"The transfer date cannot be more than {backdating.CurrentValue.MaxBackdatingMonths} months in the past.");

		RuleFor(command => command.Description)
			.MaximumLength(maximumLength: 255)
			.WithMessage(errorMessage: "The description cannot exceed 255 characters.")
			.When(predicate: command => command.Description is not null);
	}
}
