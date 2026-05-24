using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transactions.Commands.IncludeTransaction;

public sealed record IncludeTransactionCommand(
	Guid UserId,
	Guid TransactionId
) : IRequest<Result<Guid, DomainException>>, IAuthorizable, IUserScopedRequest;