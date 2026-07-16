using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;
using FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;
using FinanceTracker.Application.UseCases.Account.Commands.UnarchiveAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Account.Authorization;

public sealed class AccountLoader(
	IAccountRepository accountRepository
) : IEntityLoader<ArchiveAccountCommand, Core.Domains.Account.Account, AppException>,
	IEntityLoader<UnarchiveAccountCommand, Core.Domains.Account.Account, AppException>,
	IEntityLoader<RenameAccountCommand, Core.Domains.Account.Account, AppException>
{
	public Task<Result<Core.Domains.Account.Account, AppException>> LoadAsync(
		ArchiveAccountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Account.Account, AppException>> LoadAsync(
		UnarchiveAccountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Account.Account, AppException>> LoadAsync(
		RenameAccountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	private async Task<Result<Core.Domains.Account.Account, AppException>> LoadAndAuthorize(Guid accountId, Guid userId, CancellationToken ct)
	{
		Core.Domains.Account.Account? account = await accountRepository.GetByIdAsync(accountId: accountId, ct: ct);
		if (account is null || account.UserId != userId)
			return Result<Core.Domains.Account.Account, AppException>.Failure(error: new NotFoundException(message: "Account not found.", id: accountId));

		return Result<Core.Domains.Account.Account, AppException>.Success(value: account);
	}
}
