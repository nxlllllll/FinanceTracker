using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;

public sealed class UnarchiveAccountHandler(
	IAccountRepository accountRepository,
	IDateProvider dateProvider
) : IAuthorizedHandler<UnarchiveAccountCommand, Account>
{
	public async Task HandleAsync(
		UnarchiveAccountCommand command,
		Account account,
		CancellationToken ct = default)
	{
		account.Unarchive(occurredAt: dateProvider.UtcNow);
		await accountRepository.SaveAsync(account: account, ct: ct);
	}
}