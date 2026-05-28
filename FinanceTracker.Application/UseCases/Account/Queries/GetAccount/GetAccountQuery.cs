using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.ReadModels;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Queries.GetAccount;

public sealed record GetAccountQuery(
	Guid AccountId,
	Guid UserId
) : IRequest<AccountReadModel?>, IUserScopedRequest;