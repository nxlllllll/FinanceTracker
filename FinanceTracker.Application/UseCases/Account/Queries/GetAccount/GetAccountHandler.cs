using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Queries.GetAccount;

public sealed class GetAccountHandler(
	IAccountReadRepository accountReadRepository
) : IRequestHandler<GetAccountQuery, Core.Domains.Account.Account?>
{
	public async Task<Core.Domains.Account.Account?> Handle(
		GetAccountQuery query,
		CancellationToken ct
	) => await accountReadRepository.GetByIdAsync(accountId: query.AccountId, userId: query.UserId, ct: ct);
}
