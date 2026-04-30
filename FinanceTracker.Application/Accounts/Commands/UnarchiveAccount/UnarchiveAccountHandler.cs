using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Account;

namespace FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;

public sealed class UnarchiveAccountHandler(
	IAccountRepository accountRepository
) : IAuthorizedHandler<UnarchiveAccountCommand, Account>
{
	public async Task HandleAsync(
		UnarchiveAccountCommand command,
		Account account,
		CancellationToken ct = default)
	{
		account.Unarchive();
		await accountRepository.SaveAsync(account: account, ct: ct);
	}
}