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
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transactions.Authorization;

public sealed class TransactionLoader(
	IAccountRepository accountRepository,
	ICategoryReadRepository categoryRepository,
	ITransactionReadRepository transactionReadRepository
) : IEntityLoader<CreateTransactionCommand, Account, DomainException>,
	IEntityLoader<ChangeTransactionCategoryCommand, Transaction, DomainException>,
	IEntityLoader<ChangeTransactionDescriptionCommand, Transaction, DomainException>,
	IEntityLoader<IncludeTransactionCommand, Transaction, DomainException>,
	IEntityLoader<ExcludeTransactionCommand, Transaction, DomainException>
{
	public async Task<Result<Account, DomainException>> LoadAsync(
		CreateTransactionCommand request,
		CancellationToken ct)
	{
		Result<Account, DomainException> account = await LoadAccountAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);
		if (account.IsFailure)
			return Result<Account, DomainException>.Failure(error: account.Error!);
		
		DomainException? exception = await ValidateCategoryDirection(categoryId: request.CategoryId, direction: request.Direction, ct: ct);
		if (exception is not null)
			return Result<Account, DomainException>.Failure(error: exception);
			
		return account;
	}

	public async Task<Result<Transaction, DomainException>> LoadAsync(
		ChangeTransactionCategoryCommand request,
		CancellationToken ct)
	{
		Result<Transaction, DomainException> transaction = await LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);
		if (transaction.IsFailure)
			return Result<Transaction, DomainException>.Failure(error: transaction.Error!);

		DomainException? exception = await ValidateCategoryDirection(categoryId: request.CategoryId, direction: transaction.Value!.Direction, ct: ct);
		if (exception is not null)
			return Result<Transaction, DomainException>.Failure(error: exception);

		return transaction;
	}

	public Task<Result<Transaction, DomainException>> LoadAsync(
		ChangeTransactionDescriptionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Transaction, DomainException>> LoadAsync(
		IncludeTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Transaction, DomainException>> LoadAsync(
		ExcludeTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);
	
	private async Task<Result<Transaction, DomainException>> LoadAndAuthorize(Guid transactionId, Guid userId, CancellationToken ct)
	{
		Transaction? transaction = await transactionReadRepository.GetByIdAsync(transactionId: transactionId, ct: ct);
		if (transaction is null || transaction.UserId != userId)
			return Result<Transaction, DomainException>.Failure(error: new NotFoundException(message: "Transaction not found.", id: transactionId));
		
		return Result<Transaction, DomainException>.Success(value: transaction);
	}
	
	private async Task<Result<Account, DomainException>> LoadAccountAndAuthorize(Guid accountId, Guid userId, CancellationToken ct)
	{
		Account? account = await accountRepository.GetByIdAsync(accountId: accountId, ct: ct);
		if (account is null || account.UserId != userId)
			return Result<Account, DomainException>.Failure(error: new NotFoundException(message: "Account not found.", id: accountId));

		return Result<Account, DomainException>.Success(value: account);
	}
	
	private async Task<DomainException?> ValidateCategoryDirection(
		Guid categoryId,
		DirectionType direction,
		CancellationToken ct)
	{
		Category? category = await categoryRepository.GetByIdAsync(categoryId: categoryId, ct: ct);
		if (category is null)
			return new NotFoundException(message: "Category not found.", id: categoryId);
		
		bool valid = (direction, category.Type) switch
		{
			(DirectionType.Debit,  CategoryType.Expense) => true,
			(DirectionType.Credit, CategoryType.Income)  => true,
			_ => false
		};

		if (!valid)
			return new InvalidTransactionDirectionException(message: $"Direction '{direction}' is not compatible with category type '{category.Type}'.");

		return null;
	}
}