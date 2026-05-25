using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.Accounts.Queries.GetAccount;

public sealed record GetAccountQuery(Guid AccountId, Guid UserId) : IRequest<AccountDto?>, IUserScopedRequest;
