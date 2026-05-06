using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Services.DateProvider;
using Microsoft.Extensions.Logging;
using Quartz;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.Jobs.RecurringTransaction;

[DisallowConcurrentExecution]
public sealed class RecurringTransactionHandlingJob(
	IRecurringTransactionReadRepository recurringTransactionReadRepository,
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	INotificationDispatcher notificationDispatcher,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	ITransactionNotificationFactory factory,
	ILogger<RecurringTransactionHandlingJob> logger
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
			await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				IAppNotification appNotification = factory.Build(
					accountId: dueTransaction.AccountId,
					userId: dueTransaction.UserId,
					categoryId: dueTransaction.CategoryId,
					amount: dueTransaction.Amount.Amount,
					currency: dueTransaction.Amount.Currency,
					direction: dueTransaction.Direction,
					description: dueTransaction.Description,
					occurredAt: now
				);
				
				await notificationDispatcher.DispatchAsync(appNotification: appNotification, ct: ct);

				await recurringTransactionWriteRepository.MarkExecutedAsync(
					recurringTransactionId: dueTransaction.Id,
					executedAt: now,
					ct: ct
				);
				
			}, 
			onError: async exception => logger.ZLogError(message: $"Failed to process recurring transaction {dueTransaction.Id}: {exception.Message}"),
			ct: ct);
		}
	}
	
    public async Task Execute(IJobExecutionContext context)
		=> await ProcessTransactionsAsync(ct: context.CancellationToken);
}