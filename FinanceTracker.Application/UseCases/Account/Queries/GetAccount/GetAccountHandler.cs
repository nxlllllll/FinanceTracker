using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.UseCases.Account.Queries.GetAccount;

public sealed class GetAccountHandler(
	IAccountReadRepository accountReadRepository
) : IRequestHandler<GetAccountQuery, AccountReadModel?>
{
	public async Task<AccountReadModel?> Handle(
		GetAccountQuery query,
		CancellationToken ct
	) => await accountReadRepository.GetByIdAsync(accountId: query.AccountId, userId: query.UserId, ct: ct);
}