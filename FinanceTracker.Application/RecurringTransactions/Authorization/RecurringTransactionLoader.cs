using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.RecurringTransactions.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Authorization;

public sealed class RecurringTransactionLoader(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IEntityLoader<ActivateRecurringTransactionCommand, RecurringTransactionDto>,
	IEntityLoader<ChangeRecurringTransactionAmountCommand, RecurringTransactionDto>,
	IEntityLoader<ChangeRecurringTransactionCurrencyCommand, RecurringTransactionDto>,
	IEntityLoader<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransactionDto>,
	IEntityLoader<DeactivateRecurringTransactionCommand, RecurringTransactionDto>
{
	public Task<RecurringTransactionDto> LoadAsync(
		ActivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<RecurringTransactionDto> LoadAsync(
		ChangeRecurringTransactionAmountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<RecurringTransactionDto> LoadAsync(
		ChangeRecurringTransactionCurrencyCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<RecurringTransactionDto> LoadAsync(
		ChangeRecurringTransactionDayOfMonthCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<RecurringTransactionDto> LoadAsync(
		DeactivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	private async Task<RecurringTransactionDto> LoadAndAuthorize(Guid recurringTransactionId, Guid userId, CancellationToken ct)
	{
		RecurringTransactionDto recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: recurringTransactionId, ct: ct)
			?? throw new NotFoundException(message: "Recurring transaction not found.", id: recurringTransactionId);
		
		if (recurringTransaction.UserId != userId)
			throw new NotFoundException(message: "Recurring transaction not found.", id: recurringTransactionId);

		return recurringTransaction;
	}
}