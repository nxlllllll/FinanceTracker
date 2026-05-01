using FinanceTracker.Core.Services.DateProvider;
using FluentValidation;

namespace FinanceTracker.Application.Transfers.Commands;

public sealed class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
	public CreateTransferCommandValidator(IDateProvider dateProvider)
	{
		RuleFor(command => command.Amount)
			.GreaterThan(valueToCompare: 0).WithMessage(errorMessage: "The transfer amount must be greater than zero.");

		RuleFor(command => command.FromAccountId)
			.NotEmpty().WithMessage(errorMessage: "The source account cannot be empty.");

		RuleFor(command => command.CurrencyFrom)
			.NotEmpty().WithMessage(errorMessage: "The destination account cannot be empty.");

		RuleFor(command => command.ToAccountId)
			.NotEmpty().WithMessage(errorMessage: "The destination account cannot be empty.");

		RuleFor(command => command.CurrencyTo)
			.NotEmpty().WithMessage(errorMessage: "The source account cannot be empty.");

		RuleFor(command => command.UserId)
			.NotEmpty().WithMessage(errorMessage: "The user cannot be empty.");

		RuleFor(command => command.OccurredAt)
			.NotEmpty().WithMessage(errorMessage: "The transfer date cannot be empty.")
			.Must(predicate: date => date <= dateProvider.UtcNow)
			.WithMessage(errorMessage: "The transfer date cannot be in the future.");

		RuleFor(command => command.Description)
			.MaximumLength(maximumLength: 255)
			.WithMessage(errorMessage: "The description cannot exceed 255 characters.")
			.When(predicate: command => command.Description is not null);

		RuleFor(command => command)
			.Must(predicate: command => command.FromAccountId != command.ToAccountId)
			.WithName(overridePropertyName: "ToAccountId")
			.WithMessage(errorMessage: "The source and destination accounts must be different.");
	}
}