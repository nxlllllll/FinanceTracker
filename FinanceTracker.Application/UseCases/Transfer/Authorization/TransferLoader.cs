using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transfer.Commands;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transfer.Authorization;

public sealed class TransferLoader(
	IAccountRepository accountRepository
) : IEntityLoader<CreateTransferCommand, (Core.Domains.Account.Account, Core.Domains.Account.Account), DomainException>
{
	public Task<Result<(Core.Domains.Account.Account, Core.Domains.Account.Account), DomainException>> LoadAsync(
		CreateTransferCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(toAccountId: request.ToAccountId, fromAccountId: request.FromAccountId, userId: request.UserId, ct: ct);
	
	private async Task<Result<(Core.Domains.Account.Account, Core.Domains.Account.Account), DomainException>> LoadAndAuthorize(
		Guid toAccountId,
		Guid fromAccountId,
		Guid userId,
		CancellationToken ct)
	{
		if (fromAccountId == toAccountId)
			return Result<(Core.Domains.Account.Account, Core.Domains.Account.Account), DomainException>.Failure(error: new SameAccountTransferException(message: "Cannot transfer to the same account."));

		Core.Domains.Account.Account? fromAccount = await accountRepository.GetByIdAsync(accountId: fromAccountId, ct: ct);
		if (fromAccount is null || fromAccount.UserId != userId)
			return Result<(Core.Domains.Account.Account, Core.Domains.Account.Account), DomainException>.Failure(error: new NotFoundException(message: "Source account not found.", id: fromAccountId));

		Core.Domains.Account.Account? toAccount = await accountRepository.GetByIdAsync(accountId: toAccountId, ct: ct);
		if (toAccount is null || toAccount.UserId != userId)
			return Result<(Core.Domains.Account.Account, Core.Domains.Account.Account), DomainException>.Failure(error: new NotFoundException(message: "Destination account not found.", id: toAccountId));

		return Result<(Core.Domains.Account.Account, Core.Domains.Account.Account), DomainException>.Success(value: (fromAccount, toAccount));
	}
}
