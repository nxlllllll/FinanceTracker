using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetAccounts;

public sealed record GetAccountsQuery(
	Guid UserId,
	bool? IsArchived = null
) : IRequest<IReadOnlyList<AccountDto>>, IUserScopedRequest;