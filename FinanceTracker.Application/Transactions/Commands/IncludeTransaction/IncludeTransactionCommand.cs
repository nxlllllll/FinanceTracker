using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.IncludeTransaction;

public sealed record IncludeTransactionCommand(
	Guid UserId,
	Guid TransactionId
) : IRequest<Guid>, IAuthorizable;