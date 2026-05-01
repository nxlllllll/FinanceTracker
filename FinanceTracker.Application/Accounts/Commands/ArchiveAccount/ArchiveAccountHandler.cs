using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.Accounts.Commands.ArchiveAccount;

public sealed class ArchiveAccountHandler(
	IAccountRepository accountRepository,
	IDateProvider dateProvider
) : IAuthorizedHandler<ArchiveAccountCommand, Account>
{
	public async Task HandleAsync(
		ArchiveAccountCommand command,
		Account account,
		CancellationToken ct = default)
	{
		account.Archive(occurredAt: dateProvider.UtcNow);
		await accountRepository.SaveAsync(account: account, ct: ct);
	}
}