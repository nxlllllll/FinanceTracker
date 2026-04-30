using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.Transactions.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;
using FinanceTracker.Application.Transactions.Commands.IncludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;

namespace FinanceTracker.Application.Transactions.Authorization;

public sealed class TransactionLoader(
	IAccountRepository accountRepository,
	ITransactionReadRepository transactionReadRepository
) : IEntityLoader<CreateTransactionCommand, Account>,
	IEntityLoader<ChangeTransactionCategoryCommand, TransactionDto>,
	IEntityLoader<ChangeTransactionDescriptionCommand, TransactionDto>,
	IEntityLoader<IncludeTransactionCommand, TransactionDto>,
	IEntityLoader<ExcludeTransactionCommand, TransactionDto>
{
	public Task<Account> LoadAsync(
		CreateTransactionCommand request,
		CancellationToken ct
	) => LoadAccountAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	public Task<TransactionDto> LoadAsync(
		ChangeTransactionCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<TransactionDto> LoadAsync(
		ChangeTransactionDescriptionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<TransactionDto> LoadAsync(
		IncludeTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<TransactionDto> LoadAsync(
		ExcludeTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);
	
	private async Task<TransactionDto> LoadAndAuthorize(Guid transactionId, Guid userId, CancellationToken ct)
	{
		TransactionDto transaction = await transactionReadRepository.GetByIdAsync(transactionId: transactionId, ct: ct)
		?? throw new NotFoundException(message: "Transaction not found.", id: transactionId);
		
		if (transaction.UserId != userId)
			throw new NotFoundException(message: "Transaction not found.", id: transactionId);
		
		return transaction;
	}

	private async Task<Account> LoadAccountAndAuthorize(Guid accountId, Guid userId, CancellationToken ct)
	{
		Account account = await accountRepository.GetByIdAsync(accountId: accountId, ct: ct) 
		?? throw new NotFoundException(message: "Account not found.", id: accountId);

		if (account.UserId != userId)
			throw new NotFoundException(message: "Account not found.", id: accountId);

		return account;
	}
}