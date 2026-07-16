using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ExcludeTransaction;

public sealed record ExcludeTransactionCommand(
	Guid UserId,
	Guid TransactionId
) : IRequest<Result<Guid, AppException>>, IAuthorizable, IUserScopedRequest;
