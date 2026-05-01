using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Services.DateProvider;
using Quartz;

namespace FinanceTracker.Infrastructure.Database.Jobs.RecurringTransaction;

[DisallowConcurrentExecution]
public sealed class RecurringTransactionHandlingJob(
	IRecurringTransactionReadRepository recurringTransactionReadRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	INotificationDispatcher notificationDispatcher,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IJob
{
	internal async Task ProcessTransactionsAsync(CancellationToken ct)
	{
		DateTime now = dateProvider.UtcNow;
		DateTime firstDayOfCurrentMonth = new DateTime(
			year: now.Year,
			month: now.Month,
			day: 1,
			hour: 0,
			minute: 0,
			second: 0,
			kind: DateTimeKind.Utc
		);
		
		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> dueTransactions = await recurringTransactionReadRepository.GetDueTodayAsync(
			dayOfMonth: now.Day, 
			daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
			currentMonthStart: firstDayOfCurrentMonth,
			ct: ct
		);

		if (dueTransactions.Count == 0)
			return;

		foreach (Core.Domains.RecurringTransaction.RecurringTransaction dueTransaction in dueTransactions)
		{
			try
			{
				await unitOfWork.BeginTransactionAsync(ct: ct);
				
				await notificationDispatcher.DispatchAsync(new Notification(Data: new RecurringTransactionNotification(
					AccountId: dueTransaction.AccountId,
					UserId: dueTransaction.UserId,
					CategoryId: dueTransaction.CategoryId,
					Amount: dueTransaction.Amount.Amount,
					Currency: dueTransaction.Amount.Currency,
					Direction: dueTransaction.Direction,
					Description: dueTransaction.Description,
					OccurredAt: now
				)), ct: ct);

				await recurringTransactionWriteRepository.MarkExecutedAsync(
					recurringTransactionId: dueTransaction.Id,
					executedAt: now,
					ct: ct
				);
				
				await unitOfWork.CommitAsync(ct: ct);
			}
			catch
			{
				await unitOfWork.RollbackAsync(ct: ct);
			}
		}
	}
	
    public async Task Execute(IJobExecutionContext context)
		=> await ProcessTransactionsAsync(ct: context.CancellationToken);
}