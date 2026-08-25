using FluentValidation;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.CancelTransaction;

public sealed class CancelTransactionCommandValidator : AbstractValidator<CancelTransactionCommand>
{
	public CancelTransactionCommandValidator()
	{
		RuleFor(expression: x => x.UserId).NotEmpty();
		RuleFor(expression: x => x.TransactionId).NotEmpty();
	}
}
