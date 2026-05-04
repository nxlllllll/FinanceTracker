using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ActivateRecurringTransaction;

public sealed record ActivateRecurringTransactionCommand(
	Guid UserId,
	Guid RecurringTransactionId
) : IRequest<Result<Guid, DomainException>>, IAuthorizable;