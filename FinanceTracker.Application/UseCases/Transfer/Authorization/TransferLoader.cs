using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transfer.Commands.CreateTransfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transfer.Authorization;

public sealed class TransferLoader(
	IAccountRepository accountRepository
) : IEntityLoader<CreateTransferCommand, TransferAccount, AppException>
{
	public Task<Result<TransferAccount, AppException>> LoadAsync(
		CreateTransferCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(toAccountId: request.ToAccountId, fromAccountId: request.FromAccountId, userId: request.UserId, ct: ct);

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
