using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;

public sealed class ArchiveAccountHandler(
	IAccountRepository accountRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IAuthorizedHandler<ArchiveAccountCommand, Core.Domains.Account.Account, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ArchiveAccountCommand command,
		Core.Domains.Account.Account accounts,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = accounts.Archive(occurredAt: dateProvider.UtcNow);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await accountRepository.SaveAsync(account: accounts, ct: ct), ct: ct);

		return Result<Guid, DomainException>.Success(value: accounts.Id);
	}
}