using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;

public sealed record CreateTransactionCommand(
    Guid AccountId,
    Guid UserId,
    Guid CategoryId,
    decimal Amount,
    Currency Currency,
    DirectionType Direction,
    string? Description,
    DateTime OccurredAt
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;