using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.RecurringTransactions.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Authorization;

public sealed class RecurringTransactionLoader(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IEntityLoader<ActivateRecurringTransactionCommand, RecurringTransaction>,
	IEntityLoader<ChangeRecurringTransactionAmountCommand, RecurringTransaction>,
	IEntityLoader<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction>,
	IEntityLoader<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction>,
	IEntityLoader<DeactivateRecurringTransactionCommand, RecurringTransaction>
{
	public Task<RecurringTransaction> LoadAsync(
		ActivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<RecurringTransaction> LoadAsync(
		ChangeRecurringTransactionAmountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<RecurringTransaction> LoadAsync(
		ChangeRecurringTransactionCurrencyCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<RecurringTransaction> LoadAsync(
		ChangeRecurringTransactionDayOfMonthCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<RecurringTransaction> LoadAsync(
		DeactivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	private async Task<RecurringTransaction> LoadAndAuthorize(Guid recurringTransactionId, Guid userId, CancellationToken ct)
	{
		RecurringTransaction recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: recurringTransactionId, ct: ct)
			?? throw new NotFoundException(message: "Recurring transaction not found.", id: recurringTransactionId);
		
		if (recurringTransaction.UserId != userId)
			throw new NotFoundException(message: "Recurring transaction not found.", id: recurringTransactionId);

		return recurringTransaction;
	}
}