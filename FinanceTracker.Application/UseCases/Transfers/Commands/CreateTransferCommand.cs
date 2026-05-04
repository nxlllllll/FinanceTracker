using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transfers.Commands;

public sealed record CreateTransferCommand(
	Guid UserId,
	Guid FromAccountId,
	string CurrencyFrom,
	Guid ToAccountId,
	string CurrencyTo,
	decimal Amount,
	string? Description,
	DateTime OccurredAt
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;