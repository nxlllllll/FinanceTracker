using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.Accounts.Commands.ArchiveAccount;

public sealed class ArchiveAccountHandler(
	IAccountRepository accountRepository,
	IDateProvider dateProvider
) : IAuthorizedHandler<ArchiveAccountCommand, Account, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ArchiveAccountCommand command,
		Account account,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = account.Archive(occurredAt: dateProvider.UtcNow);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await accountRepository.SaveAsync(account: account, ct: ct);
		return Result<Guid, DomainException>.Success(value: account.Id);
	}
}
