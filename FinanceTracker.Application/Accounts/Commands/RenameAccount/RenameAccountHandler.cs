using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Account;

namespace FinanceTracker.Application.Accounts.Commands.RenameAccount;

public sealed class RenameAccountHandler(
	IAccountRepository accountRepository
) : IAuthorizedHandler<RenameAccountCommand, Account>
{
	public async Task HandleAsync(
		RenameAccountCommand command,
		Account account,
		CancellationToken ct = default)
	{
		account.Rename(newName: command.NewName);
		await accountRepository.SaveAsync(account: account, ct: ct);
	}
}