using FinanceTracker.Core.Domains.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.CreateTransaction;

public sealed record CreateTransactionCommand(
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	DirectionType Direction,
	decimal ExchangeRate,
	string? Description,
	DateTime OccurredAt
) : IRequest<Guid>;