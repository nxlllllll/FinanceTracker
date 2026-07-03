using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetTotalBalance;

public sealed record GetTotalBalanceQuery(Guid UserId) : IRequest<Money>, IUserScopedRequest;
