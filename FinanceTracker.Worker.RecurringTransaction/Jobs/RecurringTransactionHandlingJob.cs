using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.RecurringTransaction.Jobs;

[DisallowConcurrentExecution]
public sealed class RecurringTransactionHandlingJob(
    IRecurringTransactionReadRepository recurringTransactionReadRepository,
    IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
    IRabbitMqPublisher publisher,
    ICorrelationContext correlationContext,
    IDateProvider dateProvider,
    ILogger<RecurringTransactionHandlingJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext executionContext)
        => await ProcessTransactionsAsync(ct: executionContext.CancellationToken);

    private async Task ProcessTransactionsAsync(CancellationToken ct)
    {
        IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> dueTransactions = await GetDueTransactionsAsync(ct: ct);

        if (dueTransactions.Count == 0)
            return;

        logger.ZLogInformation(message: $"[{correlationContext.CorrelationId}] Found {dueTransactions.Count} due recurring transaction(s) for {dateProvider.UtcNow:dd.MM.yyyy}.");

        int processed = 0;
        int failed = 0;

        foreach (Core.Domains.RecurringTransaction.RecurringTransaction transaction in dueTransactions)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                await PublishAsync(transaction: transaction, ct: ct);
                await MarkExecutedAsync(transaction: transaction, ct: ct);

                logger.ZLogInformation(message: $"[{correlationContext.CorrelationId}] Processed: {++processed}/{dueTransactions.Count} (id: {transaction.Id}).");
            }
            catch (Exception ex)
            {
                failed++;
                logger.ZLogError(exception: ex, message: $"[{correlationContext.CorrelationId}] Failed to process recurring transaction {transaction.Id}. Skipping.");
            }
        }

        if (failed > 0)
            logger.ZLogWarning(message: $"[{correlationContext.CorrelationId}] Completed with {failed} failure(s) out of {dueTransactions.Count}.");
    }

    private async Task<IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction>> GetDueTransactionsAsync(CancellationToken ct)
    {
        DateTime now = dateProvider.UtcNow;
        DateTime currentMonthStart = new DateTime(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

        logger.ZLogInformation(message: $"Querying due transactions for day {now.Day}, month start: {currentMonthStart:O}.");

        return await recurringTransactionReadRepository.GetDueTodayAsync(
            dayOfMonth: now.Day,
            daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
            currentMonthStart: currentMonthStart,
            ct: ct
        );
    }

    private async Task PublishAsync(
        Core.Domains.RecurringTransaction.RecurringTransaction transaction,
        CancellationToken ct)
    {
        await publisher.PublishAsync(message: new RecurringTransactionTriggeredMessage(
            MessageId: Guid.CreateVersion7(),
            RecurringTransactionId: transaction.Id,
            AccountId: transaction.AccountId,
            UserId: transaction.UserId,
            CategoryId: transaction.CategoryId,
            Amount: transaction.Amount.Amount,
            Currency: transaction.Amount.Currency,
            Direction: transaction.Direction.ToString(),
            Description: transaction.Description,
            OccurredAt: dateProvider.UtcNow,
            CorrelationId: correlationContext.CorrelationId
        ), correlationId: correlationContext.CorrelationId, ct: ct);
    }

    private async Task MarkExecutedAsync(
        Core.Domains.RecurringTransaction.RecurringTransaction transaction,
        CancellationToken ct)
    {
        await recurringTransactionWriteRepository.MarkExecutedAsync(
            recurringTransactionId: transaction.Id,
            executedAt: dateProvider.UtcNow,
            ct: ct
        );
    }
}