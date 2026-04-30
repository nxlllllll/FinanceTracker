using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Transfers.Commands;

public sealed record CreateTransferCommand(
	Guid UserId,
	Guid FromAccountId,
	string CurrencyFrom,
	Guid ToAccountId,
	string CurrencyTo,
	decimal Amount,
	string? Description,
	DateTime OccurredAt
) : IRequest<Guid>, IAuthorizable;