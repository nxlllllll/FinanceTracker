using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.ArchiveAccount;

public sealed class ArchiveAccountHandler(
	IAccountRepository accountRepository,
	IAccountWriteRepository accountWriteRepository
) : IRequestHandler<ArchiveAccountCommand>
{
	public async Task Handle(
		ArchiveAccountCommand command,
		CancellationToken ct = default)
	{
		Account account = await accountRepository.GetByIdAsync(accountId: command.AccountId, ct: ct)
			?? throw new NotFoundException(message: "Account not found.", id: command.AccountId);

		if (account.UserId != command.UserId)
			throw new NotFoundException(message: "Account not found.", id: command.AccountId);	
		
		bool changed = account.Archive();

		if (changed)
			await accountWriteRepository.ArchiveAsync(accountId: command.AccountId, ct: ct);
	}
}