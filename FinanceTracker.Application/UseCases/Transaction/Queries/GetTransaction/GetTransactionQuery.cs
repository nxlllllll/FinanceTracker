using FinanceTracker.Application.Behaviours.RateLimit;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Queries.GetTransaction;

public sealed record GetTransactionQuery(Guid TransactionId, Guid UserId) : IRequest<Core.Domains.Transaction.Transaction?>, IUserScopedRequest;
