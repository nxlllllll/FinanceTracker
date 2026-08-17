using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Authorization;

public sealed class RecurringTransactionLoader(
	IRecurringTransactionRepository recurringTransactionRepository,
	IAccountRepository accountRepository
) : IEntityLoader<CreateRecurringTransactionCommand, AppException>,
	IEntityLoader<ActivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, AppException>,
	IEntityLoader<ChangeRecurringTransactionAmountCommand, Core.Domains.RecurringTransaction.RecurringTransaction, AppException>,
	IEntityLoader<ChangeRecurringTransactionCurrencyCommand, Core.Domains.RecurringTransaction.RecurringTransaction, AppException>,
	IEntityLoader<ChangeRecurringTransactionDayOfMonthCommand, Core.Domains.RecurringTransaction.RecurringTransaction, AppException>,
	IEntityLoader<DeactivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, AppException>
{
	public async Task<Result<Unit, AppException>> LoadAsync(
		CreateRecurringTransactionCommand request,
		CancellationToken ct)
	{
		Core.Domains.Account.Account? account = await accountRepository.GetByIdAsync(accountId: request.AccountId, ct: ct);

		if (account is null || account.UserId != request.UserId)
			return Result<Unit, AppException>.Failure(error: new NotFoundException(message: "Account not found.", id: request.AccountId));

		return Result<Unit, AppException>.Success(value: Unit.Default);
	}

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, AppException>> LoadAsync(
		ActivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(id: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, AppException>> LoadAsync(
		ChangeRecurringTransactionAmountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(id: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, AppException>> LoadAsync(
		ChangeRecurringTransactionCurrencyCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(id: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, AppException>> LoadAsync(
		ChangeRecurringTransactionDayOfMonthCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(id: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, AppException>> LoadAsync(
		DeactivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(id: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	private async Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, AppException>> LoadAndAuthorize(
		Guid id,
		Guid userId,
		CancellationToken ct)
	{
		Core.Domains.RecurringTransaction.RecurringTransaction? rt = await recurringTransactionRepository.GetByIdAsync(recurringTransactionId: id, ct: ct);

		if (rt is null || rt.UserId != userId)
			return Result<Core.Domains.RecurringTransaction.RecurringTransaction, AppException>.Failure(error: new NotFoundException(message: "Recurring transaction not found.", id: id));

		return Result<Core.Domains.RecurringTransaction.RecurringTransaction, AppException>.Success(value: rt);
	}
}
