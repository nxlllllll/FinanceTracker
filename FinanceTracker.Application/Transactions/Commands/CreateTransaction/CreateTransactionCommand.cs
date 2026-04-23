using FinanceTracker.Core.Domains.Account;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.CreateTransaction;

public sealed record CreateTransactionCommand(
    Guid AccountId,
    Guid UserId,
    Guid CategoryId,
    decimal Amount,
    DirectionType Direction,
    string? Description,
    DateTime OccurredAt
) : IRequest<Guid>;