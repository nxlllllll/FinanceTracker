using MediatR;

namespace FinanceTracker.Application.Transfers.Commands;

public sealed record CreateTransferCommand(
	Guid UserId,
	Guid FromAccountId,
	Guid ToAccountId,
	decimal Amount,
	string? Description,
	DateTime OccurredAt
) : IRequest<Guid>;