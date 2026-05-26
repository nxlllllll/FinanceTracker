using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Authorization;

public sealed class RecurringTransactionLoader(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IEntityLoader<ActivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>,
	IEntityLoader<ChangeRecurringTransactionAmountCommand, Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>,
	IEntityLoader<ChangeRecurringTransactionCurrencyCommand, Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>,
	IEntityLoader<ChangeRecurringTransactionDayOfMonthCommand, Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>,
	IEntityLoader<DeactivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>
{
	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>> LoadAsync(
		ActivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>> LoadAsync(
		ChangeRecurringTransactionAmountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>> LoadAsync(
		ChangeRecurringTransactionCurrencyCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>> LoadAsync(
		ChangeRecurringTransactionDayOfMonthCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>> LoadAsync(
		DeactivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(recurringTransactionId: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	private async Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>> LoadAndAuthorize(Guid recurringTransactionId, Guid userId, CancellationToken ct)
	{
		Core.Domains.RecurringTransaction.RecurringTransaction? recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: recurringTransactionId, ct: ct);
		if (recurringTransaction is null || recurringTransaction.UserId != userId)
			return Result<Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>.Failure(error: new NotFoundException(message: "Recurring transaction not found.", id: recurringTransactionId));

		return Result<Core.Domains.RecurringTransaction.RecurringTransaction, NotFoundException>.Success(value: recurringTransaction);
	}
}
