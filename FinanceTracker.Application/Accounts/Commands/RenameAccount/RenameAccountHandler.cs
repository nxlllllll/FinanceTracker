using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.Accounts.Commands.RenameAccount;

public sealed class RenameAccountHandler(
	IAccountRepository accountRepository,
	IDateProvider dateProvider
) : IAuthorizedHandler<RenameAccountCommand, Account>
{
	public async Task HandleAsync(
		RenameAccountCommand command,
		Account account,
		CancellationToken ct = default)
	{
		account.Rename(occurredAt: dateProvider.UtcNow, newName: command.NewName);
		await accountRepository.SaveAsync(account: account, ct: ct);
	}
}