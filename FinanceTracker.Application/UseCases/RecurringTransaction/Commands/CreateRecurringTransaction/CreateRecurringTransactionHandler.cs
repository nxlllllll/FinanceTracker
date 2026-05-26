using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;

public sealed class CreateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IDateProvider dateProvider
) : IAuthorizedHandler<CreateRecurringTransactionCommand, Core.Domains.Account.Account, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		CreateRecurringTransactionCommand command,
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		Result<Money, DomainException> moneyResult = Money.Positive(amount: command.Amount, currency: command.Currency);
		if (moneyResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: moneyResult.Error!);
 
		Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> rtResult = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			accountId: command.AccountId,
			categoryId: command.CategoryId,
			amount: moneyResult.Value,
			direction: command.Direction,
			dayOfMonth: command.DayOfMonth,
			description: command.Description
		);
		if (rtResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: rtResult.Error!);
 
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = rtResult.Value!;
		await recurringTransactionWriteRepository.CreateAsync(recurringTransaction: recurringTransaction, ct: ct);
		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}
