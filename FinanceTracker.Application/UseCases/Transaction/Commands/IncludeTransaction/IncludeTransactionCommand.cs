using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.IncludeTransaction;

public sealed record IncludeTransactionCommand(
	Guid UserId,
	Guid TransactionId
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
