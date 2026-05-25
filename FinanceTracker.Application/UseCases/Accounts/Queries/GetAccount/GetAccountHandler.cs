using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.UseCases.Accounts.Queries.GetAccount;

public sealed class GetAccountHandler(
	IAccountReadRepository accountReadRepository
) : IRequestHandler<GetAccountQuery, AccountDto?>
{
	public async Task<AccountDto?> Handle(
		GetAccountQuery query,
		CancellationToken ct
	) => await accountReadRepository.GetByIdAsync(accountId: query.AccountId, userId: query.UserId, ct: ct);
}
