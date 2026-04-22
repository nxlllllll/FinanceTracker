using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.Users.Queries.GetAccounts;

public sealed record GetAccountsQuery(
	Guid UserId,
	bool? IsArchived = null
) : IRequest<IReadOnlyList<AccountDto>>;