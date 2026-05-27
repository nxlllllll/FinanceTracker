using FinanceTracker.Application.Behaviours.RateLimit;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetAccounts;

public sealed record GetAccountsQuery(
	Guid UserId,
	bool? IsArchived = null
) : IRequest<IReadOnlyList<Core.Domains.Account.Account>>, IUserScopedRequest;
