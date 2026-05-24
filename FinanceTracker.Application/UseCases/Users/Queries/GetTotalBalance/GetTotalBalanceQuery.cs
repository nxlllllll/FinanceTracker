using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetTotalBalance;

public sealed record GetTotalBalanceQuery(Guid UserId) : IRequest<TotalBalanceDto>, IUserScopedRequest;