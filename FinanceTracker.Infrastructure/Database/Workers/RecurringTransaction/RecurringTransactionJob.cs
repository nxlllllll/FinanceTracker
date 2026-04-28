using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.Workers.Outbox;
using Microsoft.Extensions.Logging;
using Quartz;

namespace FinanceTracker.Infrastructure.Database.Workers.RecurringTransaction;

[DisallowConcurrentExecution]
public sealed class RecurringTransactionJob(
	IRecurringTransactionReadRepository recurringTransactionReadRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	INotificationDispatcher notificationDispatcher,
	ILogger<RecurringTransactionJob> logger,
	IUnitOfWork unitOfWork
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
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
			currentMonthStart: firstDayOfCurrentMonth,
			ct: context.CancellationToken
		);

		if (dueTransactions.Count == 0)
			return;

		foreach (RecurringTransactionDto dueTransaction in dueTransactions)
		{
			try
			{
				await unitOfWork.BeginTransactionAsync(ct: context.CancellationToken);
				
				await notificationDispatcher.DispatchAsync(new Notification(Data: new RecurringTransactionNotification(
					AccountId: dueTransaction.AccountId,
					UserId: dueTransaction.UserId,
					CategoryId: dueTransaction.CategoryId,
					Amount: dueTransaction.Amount,
					Currency: dueTransaction.Currency,
					Direction: dueTransaction.Direction,
					Description: dueTransaction.Description,
					OccurredAt: now
				)));

				await recurringTransactionWriteRepository.MarkExecutedAsync(
					recurringTransactionId: dueTransaction.Id,
					executedAt: now,
					ct: context.CancellationToken
				);
				
				await unitOfWork.CommitAsync(ct: context.CancellationToken);
			}
			catch (Exception exception)
			{
				await unitOfWork.RollbackAsync(ct: context.CancellationToken);
				logger.LogError(exception: exception, message: "Failed to created recurred transaction: {messageId}.", dueTransaction.Id);
			}
		}
	}
}