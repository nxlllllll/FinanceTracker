using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Transfers.Commands;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;

namespace FinanceTracker.Application.Transfers.Authorization;

public sealed class TransferLoader(
	IAccountRepository accountRepository
) : IEntityLoader<CreateTransferCommand, (Account, Account)>
{
	public Task<(Account, Account)> LoadAsync(
		CreateTransferCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(toAccountId: request.ToAccountId, fromAccountId: request.FromAccountId, userId: request.UserId, ct: ct);
	
	private async Task<(Account fromAccount, Account toAccount)> LoadAndAuthorize(
		Guid toAccountId,
		Guid fromAccountId,
		Guid userId,
		CancellationToken ct)
	{
		if (fromAccountId == toAccountId)
			throw new InvalidOperationException(message: "Cannot transfer to the same account.");
		
		Account fromAccount = await accountRepository.GetByIdAsync(accountId: fromAccountId, ct: ct)
		?? throw new NotFoundException(message: "Source account not found.", id: fromAccountId);

		if (fromAccount.UserId != userId)
			throw new NotFoundException(message: "Source account not found.", id: fromAccountId);
		
		Account toAccount = await accountRepository.GetByIdAsync(accountId: toAccountId, ct: ct)
		?? throw new NotFoundException(message: "Destination account not found.", id: toAccountId);

		if (toAccount.UserId != userId)
			throw new NotFoundException(message: "Destination account not found.", id: toAccountId);
		
		return (fromAccount, toAccount);
	}
}