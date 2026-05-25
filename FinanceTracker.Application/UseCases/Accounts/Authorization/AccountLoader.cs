using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Application.UseCases.Accounts.Commands.RenameAccount;
using FinanceTracker.Application.UseCases.Accounts.Commands.UnarchiveAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Accounts.Authorization;

public sealed class AccountLoader(
	IAccountRepository accountRepository
) : IEntityLoader<ArchiveAccountCommand, Account, NotFoundException>,
	IEntityLoader<UnarchiveAccountCommand, Account, NotFoundException>,
	IEntityLoader<RenameAccountCommand, Account, NotFoundException>
{
	public Task<Result<Account, NotFoundException>> LoadAsync(
		ArchiveAccountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	public Task<Result<Account, NotFoundException>> LoadAsync(
		UnarchiveAccountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	public Task<Result<Account, NotFoundException>> LoadAsync(
		RenameAccountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	private async Task<Result<Account, NotFoundException>> LoadAndAuthorize(Guid accountId, Guid userId, CancellationToken ct)
	{
		Account? account = await accountRepository.GetByIdAsync(accountId: accountId, ct: ct);
		if (account is null || account.UserId != userId)
			return Result<Account, NotFoundException>.Failure(error: new NotFoundException(message: "Account not found.", id: accountId));

		return Result<Account, NotFoundException>.Success(value: account);
	}
}
