using FinanceTracker.Application.Behaviours.RateLimit;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Queries.GetAccount;

public sealed record GetAccountQuery(Guid AccountId, Guid UserId) : IRequest<Core.Domains.Account.Account?>, IUserScopedRequest;
