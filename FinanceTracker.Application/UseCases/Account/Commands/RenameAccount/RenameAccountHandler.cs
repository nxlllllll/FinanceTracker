using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;

public sealed class RenameAccountHandler(
	IAccountRepository accountRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IAuthorizedHandler<RenameAccountCommand, Core.Domains.Account.Account, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		RenameAccountCommand command,
		Core.Domains.Account.Account accounts,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = accounts.Rename(occurredAt: dateProvider.UtcNow, newName: command.NewName);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		if (accounts.Events.Count > 0)
			await unitOfWork.ExecuteInTransactionAsync(operation: async () => await accountRepository.SaveAsync(account: accounts, ct: ct), ct: ct);

		return Result<Guid, AppException>.Success(value: accounts.Id);
	}
}
