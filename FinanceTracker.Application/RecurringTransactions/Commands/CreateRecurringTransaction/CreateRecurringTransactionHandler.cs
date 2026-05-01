using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.RecurringTransactions.Commands.CreateRecurringTransaction;

public sealed class CreateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IDateProvider dateProvider
) : IAuthorizedHandler<CreateRecurringTransactionCommand, Account, Guid>
{
	public async Task<Guid> HandleAsync(
		CreateRecurringTransactionCommand command,
		Account account,
		CancellationToken ct = default)
	{
		RecurringTransaction recurringTransaction = RecurringTransaction.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			accountId: command.AccountId,
			categoryId: command.CategoryId,
			amount: Money.Positive(command.Amount, command.Currency),
			direction: command.Direction,
			dayOfMonth: command.DayOfMonth,
			description: command.Description
		);

		await recurringTransactionWriteRepository.CreateAsync(recurringTransaction: recurringTransaction, ct: ct);
		
		return recurringTransaction.Id;
	}
}