using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.RenameAccount;

public sealed class RenameAccountHandler(
	IAccountRepository accountRepository,
	IAccountWriteRepository accountWriteRepository
) : IRequestHandler<RenameAccountCommand>
{
	public async Task Handle(
		RenameAccountCommand command,
		CancellationToken ct = default)
	{
		Account account = await accountRepository.GetByIdAsync(accountId: command.AccountId, ct: ct)
			?? throw new NotFoundException(message: "Account not found.", id: command.AccountId);
		
		bool changed = account.Rename(newName: command.NewName);
		
		if (changed)
			await accountWriteRepository.RenameAsync(accountId: command.AccountId, newName: command.NewName, ct: ct);
	}
}