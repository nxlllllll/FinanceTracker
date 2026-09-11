using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transfer.Commands.CancelTransfer;
using FinanceTracker.Application.UseCases.Transfer.Commands.CreateTransfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transfer.Authorization;

public sealed class TransferLoader(
	IAccountRepository accountRepository,
	ITransferRepository transferRepository
) : IEntityLoader<CreateTransferCommand, TransferAccount, AppException>,
	IEntityLoader<CancelTransferCommand, Core.Domains.Transfer.Transfer, AppException>
{
	public Task<Result<TransferAccount, AppException>> LoadAsync(
		CreateTransferCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(toAccountId: request.ToAccountId, fromAccountId: request.FromAccountId, userId: request.UserId, ct: ct);

	public async Task<Result<Core.Domains.Transfer.Transfer, AppException>> LoadAsync(
		CancelTransferCommand request,
		CancellationToken ct)
	{
		Core.Domains.Transfer.Transfer? transfer = await transferRepository.GetByIdAsync(transferId: request.TransferId, ct: ct);

		if (transfer is null || transfer.UserId != request.UserId)
			return Result<Core.Domains.Transfer.Transfer, AppException>.Failure(error: new NotFoundException(message: "Transfer not found.", id: request.TransferId));

		return Result<Core.Domains.Transfer.Transfer, AppException>.Success(value: transfer);
	}

	private async Task<Result<TransferAccount, AppException>> LoadAndAuthorize(
		Guid toAccountId,
		Guid fromAccountId,
		Guid userId,
		CancellationToken ct)
	{
		if (fromAccountId == toAccountId)
			return Result<TransferAccount, AppException>.Failure(error: new SameAccountTransferException(message: "Cannot transfer to the same account."));

		Core.Domains.Account.Account? account = await accountRepository.GetByIdAsync(accountId: fromAccountId, ct: ct);
		if (account is null || account.UserId != userId)
			return Result<TransferAccount, AppException>.Failure(error: new NotFoundException(message: "Source account not found.", id: fromAccountId));

		Core.Domains.Account.Account? toAccount = await accountRepository.GetByIdAsync(accountId: toAccountId, ct: ct);
		if (toAccount is null || toAccount.UserId != userId)
			return Result<TransferAccount, AppException>.Failure(error: new NotFoundException(message: "Destination account not found.", id: toAccountId));

		if (toAccount.IsArchived)
			return Result<TransferAccount, AppException>.Failure(error: new ArchivedOperationException(message: "Cannot transfer to an archived account."));

		return Result<TransferAccount, AppException>.Success(value: new TransferAccount(
			FromAccount: account,
			ToAccountCurrency: toAccount.Balance.Currency
		));
	}
}
