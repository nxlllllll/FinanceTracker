using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.ExcludeTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.IncludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transaction.Authorization;

public sealed class TransactionLoader(
	IAccountRepository accountRepository,
	ICategoryReadRepository categoryRepository,
	ITransactionReadRepository transactionReadRepository
) : IEntityLoader<CreateTransactionCommand, Core.Domains.Account.Account, DomainException>,
	IEntityLoader<ChangeTransactionCategoryCommand, Core.Domains.Transaction.Transaction, DomainException>,
	IEntityLoader<ChangeTransactionDescriptionCommand, Core.Domains.Transaction.Transaction, DomainException>,
	IEntityLoader<IncludeTransactionCommand, Core.Domains.Transaction.Transaction, DomainException>,
	IEntityLoader<ExcludeTransactionCommand, Core.Domains.Transaction.Transaction, DomainException>
{
	public async Task<Result<Core.Domains.Account.Account, DomainException>> LoadAsync(
		CreateTransactionCommand request,
		CancellationToken ct)
	{
		Result<Core.Domains.Account.Account, DomainException> account = await LoadAccountAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);
		if (account.IsFailure)
			return Result<Core.Domains.Account.Account, DomainException>.Failure(error: account.Error!);
		
		DomainException? exception = await ValidateCategoryDirection(categoryId: request.CategoryId, userId: request.UserId, direction: request.Direction, ct: ct);
		if (exception is not null)
			return Result<Core.Domains.Account.Account, DomainException>.Failure(error: exception);
			
		return account;
	}

	public async Task<Result<Core.Domains.Transaction.Transaction, DomainException>> LoadAsync(
		ChangeTransactionCategoryCommand request,
		CancellationToken ct)
	{
		Result<Core.Domains.Transaction.Transaction, DomainException> transaction = await LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);
		if (transaction.IsFailure)
			return Result<Core.Domains.Transaction.Transaction, DomainException>.Failure(error: transaction.Error!);

		DomainException? exception = await ValidateCategoryDirection(categoryId: request.CategoryId, userId: request.UserId, direction: transaction.Value!.Direction, ct: ct);
		if (exception is not null)
			return Result<Core.Domains.Transaction.Transaction, DomainException>.Failure(error: exception);

		return transaction;
	}

	public Task<Result<Core.Domains.Transaction.Transaction, DomainException>> LoadAsync(
		ChangeTransactionDescriptionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Transaction.Transaction, DomainException>> LoadAsync(
		IncludeTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Transaction.Transaction, DomainException>> LoadAsync(
		ExcludeTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);
	
	private async Task<Result<Core.Domains.Transaction.Transaction, DomainException>> LoadAndAuthorize(Guid transactionId, Guid userId, CancellationToken ct)
	{
		Core.Domains.Transaction.Transaction? transaction = await transactionReadRepository.GetByIdAsync(transactionId: transactionId, userId: userId, ct: ct);
		if (transaction is null)
			return Result<Core.Domains.Transaction.Transaction, DomainException>.Failure(error: new NotFoundException(message: "Transaction not found.", id: transactionId));
		
		return Result<Core.Domains.Transaction.Transaction, DomainException>.Success(value: transaction);
	}
	
	private async Task<Result<Core.Domains.Account.Account, DomainException>> LoadAccountAndAuthorize(Guid accountId, Guid userId, CancellationToken ct)
	{
		Core.Domains.Account.Account? account = await accountRepository.GetByIdAsync(accountId: accountId, ct: ct);
		if (account is null || account.UserId != userId)
			return Result<Core.Domains.Account.Account, DomainException>.Failure(error: new NotFoundException(message: "Account not found.", id: accountId));

		return Result<Core.Domains.Account.Account, DomainException>.Success(value: account);
	}
	
	private async Task<DomainException?> ValidateCategoryDirection(
		Guid categoryId,
		Guid userId,
		DirectionType direction,
		CancellationToken ct)
	{
		Core.Domains.Category.Category? category = await categoryRepository.GetByIdAsync(categoryId: categoryId, userId: userId, ct: ct);
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
