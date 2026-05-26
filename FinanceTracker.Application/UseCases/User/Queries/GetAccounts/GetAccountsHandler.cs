using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetAccounts;

public sealed class GetAccountsHandler(
	IAccountReadRepository accountReadRepository
) : IRequestHandler<GetAccountsQuery, IReadOnlyList<AccountDto>>
{
	public async Task<IReadOnlyList<AccountDto>> Handle(
		GetAccountsQuery query,
		CancellationToken ct = default
	) => await accountReadRepository.GetAllAsync(userId: query.UserId, isArchived: query.IsArchived, ct: ct);
}
