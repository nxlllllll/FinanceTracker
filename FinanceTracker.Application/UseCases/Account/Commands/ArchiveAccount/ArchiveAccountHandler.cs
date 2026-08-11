using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;

public sealed class ArchiveAccountHandler(
	IAccountRepository accountRepository,
	ITransferReadRepository transferReadRepository,
	ITransactionReadRepository transactionReadRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IAuthorizedHandler<ArchiveAccountCommand, Core.Domains.Account.Account, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ArchiveAccountCommand command,
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> archiveResult = account.Archive(occurredAt: dateProvider.UtcNow);
		if (archiveResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: archiveResult.Error!);

		if (account.Events.Count == 0)
			return Result<Guid, AppException>.Success(value: account.Id);

		bool hasOpenTransferObligation = await transferReadRepository.HasOpenObligationAsync(accountId: account.Id, ct: ct);
		if (hasOpenTransferObligation)
			return Result<Guid, AppException>.Failure(error: new ArchivingException(message: "Cannot archive an account with an unsettled transfer."));

		bool hasPendingTransactionRate = await transactionReadRepository.HasPendingRateAsync(accountId: account.Id, ct: ct);
		if (hasPendingTransactionRate)
			return Result<Guid, AppException>.Failure(error: new ArchivingException(message: "Cannot archive an account with a pending exchange rate."));

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await accountRepository.SaveAsync(account: account, ct: ct), ct: ct);

		return Result<Guid, AppException>.Success(value: account.Id);
	}
}
