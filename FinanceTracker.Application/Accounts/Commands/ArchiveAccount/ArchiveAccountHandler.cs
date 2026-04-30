using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Account;

namespace FinanceTracker.Application.Accounts.Commands.ArchiveAccount;

public sealed class ArchiveAccountHandler(
	IAccountRepository accountRepository
) : IAuthorizedHandler<ArchiveAccountCommand, Account>
{
	public async Task HandleAsync(
		ArchiveAccountCommand command,
		Account account,
		CancellationToken ct = default)
	{
		account.Archive();
		await accountRepository.SaveAsync(account: account, ct: ct);
	}
}