using FluentValidation;

namespace FinanceTracker.Application.Transactions.Commands.IncludeTransaction;

public sealed class IncludeTransactionCommandValidator : AbstractValidator<IncludeTransactionCommand>
{
	public IncludeTransactionCommandValidator()
	{
		RuleFor(expression: command => command.TransactionId)
			.NotEmpty().WithMessage(errorMessage: "The transaction cannot be empty.");
	}
}