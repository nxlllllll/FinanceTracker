using FinanceTracker.Application.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Application.Accounts.Commands.RenameAccount;
using FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;

namespace FinanceTracker.Application.Accounts.Authorization;

public sealed class AccountLoader(
	IAccountRepository accountRepository
) : IEntityLoader<ArchiveAccountCommand, Account>,
	IEntityLoader<UnarchiveAccountCommand, Account>,
	IEntityLoader<RenameAccountCommand, Account>
{
	public Task<Account> LoadAsync(
		ArchiveAccountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	public Task<Account> LoadAsync(
		UnarchiveAccountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	public Task<Account> LoadAsync(
		RenameAccountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	private async Task<Account> LoadAndAuthorize(Guid accountId, Guid userId, CancellationToken ct)
	{
		Account account = await accountRepository.GetByIdAsync(accountId: accountId, ct: ct)
			?? throw new NotFoundException(message: "Account not found.", id: accountId);

		if (account.UserId != userId)
			throw new NotFoundException(message: "Account not found.", id: accountId);

		return account;
	}
}