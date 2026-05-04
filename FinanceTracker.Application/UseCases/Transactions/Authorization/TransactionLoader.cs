using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transactions.Commands.ExcludeTransaction;
using FinanceTracker.Application.UseCases.Transactions.Commands.IncludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;

namespace FinanceTracker.Application.UseCases.Transactions.Authorization;

public sealed class TransactionLoader(
	IAccountRepository accountRepository,
	ICategoryReadRepository categoryRepository,
	ITransactionReadRepository transactionReadRepository
) : IEntityLoader<CreateTransactionCommand, Account>,
	IEntityLoader<ChangeTransactionCategoryCommand, Transaction>,
	IEntityLoader<ChangeTransactionDescriptionCommand, Transaction>,
	IEntityLoader<IncludeTransactionCommand, Transaction>,
	IEntityLoader<ExcludeTransactionCommand, Transaction>
{
	public async Task<Account> LoadAsync(
		CreateTransactionCommand request,
		CancellationToken ct)
	{
		Task<Account> account = LoadAccountAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);
		await ValidateCategoryDirection(categoryId: request.CategoryId, direction: request.Direction, ct: ct);
		return await account;
	}

	public async Task<Transaction> LoadAsync(
		ChangeTransactionCategoryCommand request,
		CancellationToken ct)
	{
		Transaction transaction = await LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);
		await ValidateCategoryDirection(categoryId: request.CategoryId, direction: transaction.Direction, ct: ct);
		return transaction;
	}

	public Task<Transaction> LoadAsync(
		ChangeTransactionDescriptionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<Transaction> LoadAsync(
		IncludeTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<Transaction> LoadAsync(
		ExcludeTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);
	
	private async Task<Transaction> LoadAndAuthorize(Guid transactionId, Guid userId, CancellationToken ct)
	{
		Transaction transaction = await transactionReadRepository.GetByIdAsync(transactionId: transactionId, ct: ct)
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
	
	private async Task ValidateCategoryDirection(
		Guid categoryId,
		DirectionType direction,
		CancellationToken ct)
	{
		Category category = await categoryRepository.GetByIdAsync(categoryId: categoryId, ct: ct)
			?? throw new NotFoundException(message: "Category not found.", id: categoryId);
		
		bool valid = (direction, category.Type) switch
		{
			(DirectionType.Debit,  CategoryType.Expense) => true,
			(DirectionType.Credit, CategoryType.Income)  => true,
			_ => false
		};
 
		if (!valid)
			throw new InvalidTransactionDirectionException(message: $"Direction '{direction}' is not compatible with category type '{category.Type}'.");
	}
}