using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using Quartz;

namespace FinanceTracker.Infrastructure.Database.Jobs.RecurringTransaction;

[DisallowConcurrentExecution]
public sealed class RecurringTransactionHandlingJob(
	IRecurringTransactionReadRepository recurringTransactionReadRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	INotificationDispatcher notificationDispatcher,
	IUnitOfWork unitOfWork
) : IJob
{
	internal async Task ProcessTransactionsAsync(CancellationToken ct)
	{
		DateTime now = DateTime.UtcNow;
		DateTime firstDayOfCurrentMonth = new DateTime(
			year: now.Year,
			month: now.Month,
			day: 1,
			hour: 0,
			minute: 0,
			second: 0,
			kind: DateTimeKind.Utc
		);
		
		IReadOnlyList<RecurringTransactionDto> dueTransactions = await recurringTransactionReadRepository.GetDueTodayAsync(
			dayOfMonth: now.Day, 
			daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
			currentMonthStart: firstDayOfCurrentMonth,
			ct: ct
		);

		if (dueTransactions.Count == 0)
			return;

		foreach (RecurringTransactionDto dueTransaction in dueTransactions)
		{
			try
			{
				await unitOfWork.BeginTransactionAsync(ct: ct);
				
				await notificationDispatcher.DispatchAsync(new Notification(Data: new RecurringTransactionNotification(
					AccountId: dueTransaction.AccountId,
					UserId: dueTransaction.UserId,
					CategoryId: dueTransaction.CategoryId,
					Amount: dueTransaction.Amount,
					Currency: dueTransaction.Currency,
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