using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Queries.GetTransaction;

public sealed record GetTransactionQuery(
	Guid TransactionId,
	Guid UserId
) : IRequest<Result<TransactionReadModel, AppException>>, IUserScopedRequest;
