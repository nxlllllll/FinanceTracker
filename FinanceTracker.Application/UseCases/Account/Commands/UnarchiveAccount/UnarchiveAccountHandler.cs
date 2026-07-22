using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.Account.Commands.UnarchiveAccount;

public sealed class UnarchiveAccountHandler(
	IAccountRepository accountRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IAuthorizedHandler<UnarchiveAccountCommand, Core.Domains.Account.Account, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		UnarchiveAccountCommand command,
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = account.Unarchive(occurredAt: dateProvider.UtcNow);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (account.Events.Count == 0)
			return Result<Guid, AppException>.Success(value: account.Id);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await accountRepository.SaveAsync(account: account, ct: ct), ct: ct);

		return Result<Guid, AppException>.Success(value: account.Id);
	}
}
