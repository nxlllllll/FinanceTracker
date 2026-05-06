using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transfers.Commands;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transfers.Authorization;

public sealed class TransferLoader(
	IAccountRepository accountRepository
) : IEntityLoader<CreateTransferCommand, (Account, Account), DomainException>
{
	public Task<Result<(Account, Account), DomainException>> LoadAsync(
		CreateTransferCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(toAccountId: request.ToAccountId, fromAccountId: request.FromAccountId, userId: request.UserId, ct: ct);
	
	private async Task<Result<(Account, Account), DomainException>> LoadAndAuthorize(
		Guid toAccountId,
		Guid fromAccountId,
		Guid userId,
		CancellationToken ct)
	{
		if (fromAccountId == toAccountId)
			return Result<(Account, Account), DomainException>.Failure(error: new SameAccountTransferException(message: "Cannot transfer to the same account."));

		Account? fromAccount = await accountRepository.GetByIdAsync(accountId: fromAccountId, ct: ct);
		if (fromAccount is null || fromAccount.UserId != userId)
			return Result<(Account, Account), DomainException>.Failure(error: new NotFoundException(message: "Source account not found.", id: fromAccountId));

		Account? toAccount = await accountRepository.GetByIdAsync(accountId: toAccountId, ct: ct);
		if (toAccount is null || toAccount.UserId != userId)
			return Result<(Account, Account), DomainException>.Failure(error: new NotFoundException(message: "Destination account not found.", id: toAccountId));

		return Result<(Account, Account), DomainException>.Success(value: (fromAccount, toAccount));
	}
}