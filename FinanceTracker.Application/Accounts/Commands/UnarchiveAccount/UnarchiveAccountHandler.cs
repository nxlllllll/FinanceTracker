using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;

public sealed class UnarchiveAccountHandler(
	IAccountRepository accountRepository
) : IRequestHandler<UnarchiveAccountCommand>
{
	public async Task Handle(
		UnarchiveAccountCommand command,
		CancellationToken ct = default)
	{
		Account account = await accountRepository.GetByIdAsync(command.AccountId, ct)
			?? throw new NotFoundException(message: "Account not found.", id: command.AccountId);

		account.Unarchive();

		await accountRepository.SaveAsync(account: account, ct: ct);
	}
}