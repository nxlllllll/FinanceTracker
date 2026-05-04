using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;

public sealed record ChangeTransactionCategoryCommand(
	Guid UserId,
	Guid TransactionId,
	Guid CategoryId
) : IRequest<Guid>, IAuthorizable;