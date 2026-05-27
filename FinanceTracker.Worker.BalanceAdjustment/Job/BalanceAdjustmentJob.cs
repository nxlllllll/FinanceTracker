using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Utilities.Retry;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.BalanceAdjustment.Job;

[DisallowConcurrentExecution]
public sealed class BalanceAdjustmentJob(
    ITransactionReadRepository transactionReadRepository,
    ITransactionWriteRepository transactionWriteRepository,
    ITransferReadRepository transferReadRepository,
    ITransferWriteRepository transferWriteRepository,
    IAccountRepository accountRepository,
    ICurrencyRateReadRepository currencyRateReadRepository,
    IUnitOfWork unitOfWork,
    IDateProvider dateProvider,
    IOptionsMonitor<BalanceAdjustmentJobOptions> options,
    ILogger<BalanceAdjustmentJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext executionContext)
    {
        BalanceAdjustmentJobOptions currentOptions = options.CurrentValue;

        if (!currentOptions.IsEnabled)
        {
            logger.ZLogInformation(message: $"[{nameof(BalanceAdjustmentJob)}] Disabled. Skipping.");
            return;
        }

        await ProcessAsync(options: currentOptions, ct: executionContext.CancellationToken);
    }

    private async Task ProcessAsync(BalanceAdjustmentJobOptions options, CancellationToken ct)
    {
        await ProcessTransactionsAsync(options: options, ct: ct);
        await ProcessTransfersAsync(options: options, ct: ct);
    }

    private async Task<AdjustResult> TryAdjustAsync(
        Guid itemId,
        BalanceAdjustmentJobOptions options,
        Func<CancellationToken, Task<AdjustResult>> work,
        CancellationToken ct)
    {
        try
        {
            return await RetryDelayCalculator.ExecuteWithRetryAsync(
                operation: work,
                logging: (exception, attempt, delay) => logger.ZLogWarning(exception: exception, message: $"""
                    [ConcurrencyRetry] Attempt {attempt + 1}/{options.MaxRetries} failed.
                    Retrying in {delay}ms.
                    """),
                maxRetries: options.MaxRetries,
                baseDelayMs: options.BaseDelayMs,
                useJitter: options.UseJitter,
                ct: ct
            );
        }
        catch (Exception ex)
        {
            logger.ZLogError(exception: ex, message: $"Failed to adjust item {itemId}.");
            return AdjustResult.Failed;
        }
    }

    private void LogSummary(string entityName, int total, int adjusted, int skipped, int failed)
        => logger.ZLogInformation(message: $"{entityName}s complete. Total: {total}, adjusted: {adjusted}, skipped: {skipped}, failed: {failed}.");

    private async Task ProcessPendingAsync<T>(
        IReadOnlyList<T> pending,
        BalanceAdjustmentJobOptions options,
        string entityName,
        Func<T, Guid> getId,
        Func<T, decimal> getCurrentRate,
        Func<T, CancellationToken, Task<decimal?>> getNewRateAsync,
        Func<T, string> buildSkipMessage,
        Func<T, decimal, CancellationToken, Task> onRateUnchangedAsync,
        Func<T, decimal, CancellationToken, Task<AdjustResult>> onAdjustAsync,
        CancellationToken ct)
    {
        if (pending.Count == 0)
        {
            logger.ZLogInformation(message: $"No pending rate {entityName}s found.");
            return;
        }

        logger.ZLogInformation(message: $"Processing {pending.Count} pending rate {entityName}(s).");

        int adjusted = 0;
        int skipped = 0;
        int failed = 0;

        foreach (T item in pending)
        {
            if (ct.IsCancellationRequested)
                break;

            decimal? newRate = await getNewRateAsync(item, ct);

            if (newRate is null)
            {
                logger.ZLogWarning(message: $"{buildSkipMessage(item)} Skipping.");
                skipped++;
                continue;
            }

            if (newRate == getCurrentRate(item))
            {
                await onRateUnchangedAsync(item, newRate.Value, ct);
                adjusted++;
                continue;
            }

            AdjustResult result = await TryAdjustAsync(
                itemId: getId(item),
                options: options,
                work: innerCt => onAdjustAsync(item, newRate.Value, innerCt),
                ct: ct
            );

            switch (result)
            {
                case AdjustResult.Adjusted: adjusted++; break;
                case AdjustResult.Skipped: skipped++; break;
                case AdjustResult.Failed: failed++; break;
            }
        }

        LogSummary(entityName: entityName, total: pending.Count, adjusted: adjusted, skipped: skipped, failed: failed);
    }

    private async Task ProcessTransactionsAsync(BalanceAdjustmentJobOptions options, CancellationToken ct)
    {
        IReadOnlyList<PendingRateTransaction> pending = await transactionReadRepository.GetPendingRateAsync(ct: ct);

        await ProcessPendingAsync(
            pending: pending,
            options: options,
            entityName: nameof(Transaction),
            getId: item => item.TransactionId,
            getCurrentRate: item => item.CurrentRate,
            getNewRateAsync: (item, innerCt) => currencyRateReadRepository.GetRateAsync(
                baseCurrencyCode: item.TransactionCurrency,
                targetCurrencyCode: item.BaseCurrency,
                date: DateOnly.FromDateTime(dateTime: item.OccurredAt.UtcDateTime),
                ct: innerCt
            ),
            buildSkipMessage: item => $"Rate not found for transaction {item.TransactionId} ({item.TransactionCurrency} > {item.BaseCurrency} on {item.OccurredAt:d}).",
            onRateUnchangedAsync: (item, rate, innerCt) => transactionWriteRepository.UpdateRateAsync(
                transactionId: item.TransactionId, 
                newRate: rate, 
                ct: innerCt
            ),
            onAdjustAsync: async (item, newRate, innerCt) =>
            {
                Account? account = await accountRepository.GetByIdAsync(accountId: item.AccountId, ct: innerCt);

                if (account is null)
                {
                    logger.ZLogWarning(message: $"Account {item.AccountId} not found for transaction {item.TransactionId}. Skipping.");
                    return AdjustResult.Skipped;
                }

                Result<Unit, DomainException> adjustResult = account.AdjustBalance(
                    occurredAt: dateProvider.UtcNow,
                    sourceId: item.TransactionId,
                    sourceType: AggregateTypeNames.Transaction,
                    direction: item.Direction,
                    oldRate: item.CurrentRate,
                    newRate: newRate,
                    amount: item.Amount
                );

                if (adjustResult.IsFailure)
                {
                    logger.ZLogWarning(message: $"AdjustBalance failed for transaction {item.TransactionId}: {adjustResult.Error!.Message}. Skipping.");
                    return AdjustResult.Skipped;
                }

                await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
                {
                    await accountRepository.SaveAsync(account: account, ct: innerCt);
                    await transactionWriteRepository.UpdateRateAsync(transactionId: item.TransactionId, newRate: newRate, ct: innerCt);
                }, ct: innerCt);

                logger.ZLogInformation(message: $"Adjusted transaction {item.TransactionId}: rate {item.CurrentRate} > {newRate}.");
                return AdjustResult.Adjusted;
            },
            ct: ct
        );
    }

    private async Task ProcessTransfersAsync(BalanceAdjustmentJobOptions options, CancellationToken ct)
    {
        IReadOnlyList<PendingRateTransfer> pending = await transferReadRepository.GetPendingRateAsync(ct: ct);

        await ProcessPendingAsync(
            pending: pending,
            options: options,
            entityName: nameof(Transfer),
            getId: item => item.TransferId,
            getCurrentRate: item => item.CurrentRate,
            getNewRateAsync: (item, innerCt) => currencyRateReadRepository.GetRateAsync(
                baseCurrencyCode: item.CurrencyFrom,
                targetCurrencyCode: item.CurrencyTo,
                date: DateOnly.FromDateTime(dateTime: item.OccurredAt.UtcDateTime),
                ct: innerCt
            ),
            buildSkipMessage: item => $"Rate not found for transfer {item.TransferId} ({item.CurrencyFrom} > {item.CurrencyTo} on {item.OccurredAt:d}).",
            onRateUnchangedAsync: (item, rate, innerCt) => transferWriteRepository.UpdateRateAsync(
                transferId: item.TransferId,
                newRate: rate,
                ct: innerCt
            ),
            onAdjustAsync: async (item, newRate, innerCt) =>
            {
                Account? fromAccount = await accountRepository.GetByIdAsync(accountId: item.FromAccountId, ct: innerCt);
                Account? toAccount = await accountRepository.GetByIdAsync(accountId: item.ToAccountId, ct: innerCt);

                if (fromAccount is null || toAccount is null)
                {
                    logger.ZLogWarning(message: $"Account(s) not found for transfer {item.TransferId}. Skipping.");
                    return AdjustResult.Skipped;
                }

                Result<Unit, DomainException> fromResult = fromAccount.AdjustBalance(
                    occurredAt: dateProvider.UtcNow,
                    sourceId: item.TransferId,
                    sourceType: AggregateTypeNames.Transfer,
                    direction: DirectionType.Debit,
                    oldRate: item.CurrentRate,
                    newRate: newRate,
                    amount: item.AmountFrom
                );

                if (fromResult.IsFailure)
                {
                    logger.ZLogWarning(message: $"AdjustBalance failed for from-account on transfer {item.TransferId}: {fromResult.Error!.Message}. Skipping.");
                    return AdjustResult.Skipped;
                }

                Result<Unit, DomainException> toResult = toAccount.AdjustBalance(
                    occurredAt: dateProvider.UtcNow,
                    sourceId: item.TransferId,
                    sourceType: AggregateTypeNames.Transfer,
                    direction: DirectionType.Credit,
                    oldRate: item.CurrentRate,
                    newRate: newRate,
                    amount: item.AmountFrom
                );

                if (toResult.IsFailure)
                {
                    logger.ZLogWarning(message: $"AdjustBalance failed for to-account on transfer {item.TransferId}: {toResult.Error!.Message}. Skipping.");
                    return AdjustResult.Skipped;
                }

                await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
                {
                    await accountRepository.SaveAsync(account: fromAccount, ct: innerCt);
                    await accountRepository.SaveAsync(account: toAccount, ct: innerCt);
                    await transferWriteRepository.UpdateRateAsync(transferId: item.TransferId, newRate: newRate, ct: innerCt);
                }, ct: innerCt);

                logger.ZLogInformation(message: $"Adjusted transfer {item.TransferId}: rate {item.CurrentRate} > {newRate}.");
                return AdjustResult.Adjusted;
            },
            ct: ct
        );
    }
}
