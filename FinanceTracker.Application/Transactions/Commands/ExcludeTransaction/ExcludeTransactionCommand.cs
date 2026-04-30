using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;

public sealed record ExcludeTransactionCommand(
	Guid UserId,
	Guid TransactionId
) : IRequest, IAuthorizable;