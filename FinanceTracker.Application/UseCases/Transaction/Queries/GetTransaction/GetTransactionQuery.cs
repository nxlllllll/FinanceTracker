using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.ReadModels;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Queries.GetTransaction;

public sealed record GetTransactionQuery(
	Guid TransactionId,
	Guid UserId
) : IRequest<TransactionReadModel?>, IUserScopedRequest;