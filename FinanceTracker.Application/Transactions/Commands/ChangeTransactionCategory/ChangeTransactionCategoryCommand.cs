using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;

public sealed record ChangeTransactionCategoryCommand(
	Guid TransactionId,
	Guid CategoryId
) : IRequest;