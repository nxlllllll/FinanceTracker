using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.ExcludeTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.IncludeTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Transaction.Authorization;

public sealed class TransactionLoader(
	IAccountRepository accountRepository,
	ITransactionRepository transactionRepository
) : IEntityLoader<CreateTransactionCommand, Core.Domains.Account.Account, AppException>,
	IEntityLoader<ChangeTransactionCategoryCommand, Core.Domains.Transaction.Transaction, AppException>,
	IEntityLoader<ChangeTransactionDescriptionCommand, Core.Domains.Transaction.Transaction, AppException>,
	IEntityLoader<IncludeTransactionCommand, Core.Domains.Transaction.Transaction, AppException>,
	IEntityLoader<ExcludeTransactionCommand, Core.Domains.Transaction.Transaction, AppException>
{
	public async Task<Result<Core.Domains.Account.Account, AppException>> LoadAsync(
		CreateTransactionCommand request,
		CancellationToken ct
	) => await LoadAccountAndAuthorize(accountId: request.AccountId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Transaction.Transaction, AppException>> LoadAsync(
		ChangeTransactionCategoryCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Transaction.Transaction, AppException>> LoadAsync(
		ChangeTransactionDescriptionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Transaction.Transaction, AppException>> LoadAsync(
		IncludeTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Transaction.Transaction, AppException>> LoadAsync(
		ExcludeTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(transactionId: request.TransactionId, userId: request.UserId, ct: ct);

	private async Task<Result<Core.Domains.Transaction.Transaction, AppException>> LoadAndAuthorize(
		Guid transactionId,
		Guid userId,
		CancellationToken ct)
	{
		Core.Domains.Transaction.Transaction? transaction = await transactionRepository.GetByIdAsync(transactionId: transactionId, userId: userId, ct: ct);

		if (transaction is null)
			return Result<Core.Domains.Transaction.Transaction, AppException>.Failure(error: new NotFoundException(message: "Transaction not found.", id: transactionId));

		return Result<Core.Domains.Transaction.Transaction, AppException>.Success(value: transaction);
	}

	private async Task<Result<Core.Domains.Account.Account, AppException>> LoadAccountAndAuthorize(
		Guid accountId,
		Guid userId,
		CancellationToken ct)
	{
		Core.Domains.Account.Account? account = await accountRepository.GetByIdAsync(accountId: accountId, ct: ct);

		if (account is null || account.UserId != userId)
			return Result<Core.Domains.Account.Account, AppException>.Failure(error: new NotFoundException(message: "Account not found.", id: accountId));

		return Result<Core.Domains.Account.Account, AppException>.Success(value: account);
	}
}
