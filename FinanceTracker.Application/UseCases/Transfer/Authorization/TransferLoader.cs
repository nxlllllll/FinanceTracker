using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transfer.Commands;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transfer.Authorization;

public sealed class TransferLoader(
	IAccountRepository accountRepository,
	IAccountReadRepository accountReadRepository
) : IEntityLoader<CreateTransferCommand, TransferAccounts, DomainException>
{
	public Task<Result<TransferAccounts, DomainException>> LoadAsync(
		CreateTransferCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(toAccountId: request.ToAccountId, fromAccountId: request.FromAccountId, userId: request.UserId, ct: ct);

	private async Task<Result<TransferAccounts, DomainException>> LoadAndAuthorize(
		Guid toAccountId,
		Guid fromAccountId,
		Guid userId,
		CancellationToken ct)
	{
		if (fromAccountId == toAccountId)
			return Result<TransferAccounts, DomainException>.Failure(error: new SameAccountTransferException(message: "Cannot transfer to the same account."));

		Core.Domains.Account.Account? account = await accountRepository.GetByIdAsync(accountId: fromAccountId, ct: ct);
		if (account is null || account.UserId != userId)
			return Result<TransferAccounts, DomainException>.Failure(error: new NotFoundException(message: "Source account not found.", id: fromAccountId));

		AccountReadModel? toAccount = await accountReadRepository.GetByIdAsync(accountId: toAccountId, userId: userId, ct: ct);
		if (toAccount is null)
			return Result<TransferAccounts, DomainException>.Failure(error: new NotFoundException(message: "Destination account not found.", id: toAccountId));

		return Result<TransferAccounts, DomainException>.Success(value: new TransferAccounts(
			FromAccount: account,
			ToAccountCurrency: toAccount.Balance.Currency
		));
	}
}