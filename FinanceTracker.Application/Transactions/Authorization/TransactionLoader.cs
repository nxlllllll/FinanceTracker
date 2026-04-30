using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.RecurringTransactions.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.Transactions.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;
using FinanceTracker.Application.Transactions.Commands.IncludeTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.Transaction;

namespace FinanceTracker.Application.Transactions.Authorization;

public sealed class TransactionLoader(
	ITransactionReadRepository transactionReadRepository
) : IEntityLoader<ChangeTransactionCategoryCommand, TransactionDto>,
	IEntityLoader<ChangeTransactionDescriptionCommand, TransactionDto>,
	IEntityLoader<IncludeTransactionCommand, TransactionDto>,
	IEntityLoader<ExcludeTransactionCommand, TransactionDto>
{
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
}