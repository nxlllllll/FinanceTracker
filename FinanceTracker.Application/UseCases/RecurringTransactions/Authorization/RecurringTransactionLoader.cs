using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Authorization;

public sealed class RecurringTransactionLoader(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IEntityLoader<ActivateRecurringTransactionCommand, RecurringTransaction, NotFoundException>,
	IEntityLoader<ChangeRecurringTransactionAmountCommand, RecurringTransaction, NotFoundException>,
	IEntityLoader<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction, NotFoundException>,
	IEntityLoader<ChangeRecurringTransactionDayOfMonthCommand, RecurringTransaction, NotFoundException>,
	IEntityLoader<DeactivateRecurringTransactionCommand, RecurringTransaction, NotFoundException>
{
	public Task<Result<RecurringTransaction, NotFoundException>> LoadAsync(
		ActivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<RecurringTransaction, NotFoundException>> LoadAsync(
		ChangeRecurringTransactionAmountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<RecurringTransaction, NotFoundException>> LoadAsync(
		ChangeRecurringTransactionCurrencyCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<RecurringTransaction, NotFoundException>> LoadAsync(
		ChangeRecurringTransactionDayOfMonthCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<RecurringTransaction, NotFoundException>> LoadAsync(
		DeactivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	private async Task<Result<RecurringTransaction, NotFoundException>> LoadAndAuthorize(Guid recurringTransactionId, Guid userId, CancellationToken ct)
	{
		RecurringTransaction? recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: recurringTransactionId, ct: ct);
		if (recurringTransaction is null || recurringTransaction.UserId != userId)
			return Result<RecurringTransaction, NotFoundException>.Failure(error: new NotFoundException(message: "Recurring transaction not found.", id: recurringTransactionId));

		return Result<RecurringTransaction, NotFoundException>.Success(value: recurringTransaction);
	}
}