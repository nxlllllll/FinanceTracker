using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.ArchiveAccount;

public sealed class ArchiveAccountHandler(
	IAccountRepository accountRepository
) : IRequestHandler<ArchiveAccountCommand>
{
	public async Task Handle(
		ArchiveAccountCommand command,
		CancellationToken ct = default)
	{
		Account account = await accountRepository.GetByIdAsync(accountId: command.AccountId, ct: ct)
			?? throw new NotFoundException(message: "Account not found.", id: command.AccountId);

		account.Archive();

		await accountRepository.SaveAsync(account: account, ct: ct);
	}
}