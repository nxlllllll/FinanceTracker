using FluentValidation;

namespace FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;

public sealed class ExcludeTransactionCommandValidator : AbstractValidator<ExcludeTransactionCommand>
{
	public ExcludeTransactionCommandValidator()
	{
		RuleFor(expression: command => command.TransactionId)
			.NotEmpty().WithMessage(errorMessage: "The transaction cannot be empty.");
	}
}