using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Authorization;

public sealed class RecurringTransactionLoader(
	IRecurringTransactionRepository recurringTransactionRepository,
	IAccountReadRepository accountRepository
) : IEntityLoader<CreateRecurringTransactionCommand, DomainException>,
	IEntityLoader<ActivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>,
	IEntityLoader<ChangeRecurringTransactionAmountCommand, Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>,
	IEntityLoader<ChangeRecurringTransactionCurrencyCommand, Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>,
	IEntityLoader<ChangeRecurringTransactionDayOfMonthCommand, Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>,
	IEntityLoader<DeactivateRecurringTransactionCommand, Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>
{
	public async Task<Result<Unit, DomainException>> LoadAsync(
		CreateRecurringTransactionCommand request,
		CancellationToken ct)
	{
		AccountReadModel? account = await accountRepository.GetByIdAsync(accountId: request.AccountId, userId: request.UserId, ct: ct);

		if (account is null || account.UserId != request.UserId)
			return Result<Unit, DomainException>.Failure(error: new NotFoundException(message: "Account not found.", id: request.AccountId));

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>> LoadAsync(
		ActivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(id: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>> LoadAsync(
		ChangeRecurringTransactionAmountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(id: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>> LoadAsync(
		ChangeRecurringTransactionCurrencyCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(id: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>> LoadAsync(
		ChangeRecurringTransactionDayOfMonthCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(id: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>> LoadAsync(
		DeactivateRecurringTransactionCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(id: request.RecurringTransactionId, userId: request.UserId, ct: ct);

	private async Task<Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>> LoadAndAuthorize(
		Guid id,
		Guid userId,
		CancellationToken ct)
	{
		Core.Domains.RecurringTransaction.RecurringTransaction? rt = await recurringTransactionRepository.GetByIdAsync(recurringTransactionId: id, ct: ct);

		if (rt is null || rt.UserId != userId)
			return Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>.Failure(error: new NotFoundException(message: "Recurring transaction not found.", id: id));

		return Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException>.Success(value: rt);
	}
}
