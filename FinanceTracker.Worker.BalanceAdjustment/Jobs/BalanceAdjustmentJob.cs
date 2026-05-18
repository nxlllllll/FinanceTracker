using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Retry;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.BalanceAdjustment.Jobs;

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
    IOptions<BalanceAdjustmentJobOptions> options,
    ILogger<BalanceAdjustmentJob> logger
) : IJob
{
    private readonly BalanceAdjustmentJobOptions _options = options.Value;
    private static readonly Random Jitter = Random.Shared;

    public async Task Execute(IJobExecutionContext executionContext)
        => await ProcessAsync(ct: executionContext.CancellationToken);

    private async Task ProcessAsync(CancellationToken ct)
    {
        await ProcessTransactionsAsync(ct: ct);
        await ProcessTransfersAsync(ct: ct);
    }

    private async Task ExecuteWithRetryAsync(Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                await operation(ct);
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < _options.MaxRetries)
            {
                int delayMs = RetryDelayCalculator.Calculate(attempt: attempt, baseDelayMs: _options.BaseDelayMs, useJitter: _options.UseJitter);
                
                logger.ZLogWarning(message: $"[ConcurrencyRetry] Attempt {attempt + 1}/{_options.MaxRetries} failed. Retrying in {delayMs}ms.");
                await Task.Delay(millisecondsDelay: delayMs, cancellationToken: ct);
            }
        }
    }

    private async Task ProcessTransactionsAsync(CancellationToken ct)
    {
        IReadOnlyList<PendingRateTransaction> pending = await transactionReadRepository.GetPendingRateAsync(ct: ct);

        if (pending.Count == 0)
        {
            logger.ZLogInformation(message: $"No pending rate transactions found.");
            return;
        }

        logger.ZLogInformation(message: $"Processing {pending.Count} pending rate transaction(s).");

        int adjusted = 0;
        int failed = 0;

        foreach (PendingRateTransaction item in pending)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                decimal? newRate = await currencyRateReadRepository.GetRateAsync(
                    baseCurrencyCode: item.TransactionCurrency,
                    targetCurrencyCode: item.BaseCurrency,
                    date: DateOnly.FromDateTime(dateTime: item.OccurredAt),
                    ct: ct
                );

                if (newRate is null)
                {
                    logger.ZLogWarning(message: $"Rate not found for transaction {item.TransactionId} ({item.TransactionCurrency} → {item.BaseCurrency} on {item.OccurredAt:d}). Skipping.");
                    continue;
                }

                if (newRate == item.CurrentRate)
                {
                    await transactionWriteRepository.UpdateRateAsync(transactionId: item.TransactionId, newRate: newRate.Value, ct: ct);
                    adjusted++;
                    continue;
                }

                await ExecuteWithRetryAsync(operation: async innerCt =>
                {
                    Account? account = await accountRepository.GetByIdAsync(accountId: item.AccountId, ct: innerCt);

                    if (account is null)
                    {
                        logger.ZLogWarning(message: $"Account {item.AccountId} not found for transaction {item.TransactionId}. Skipping.");
                        return;
                    }

                    Result<Unit, DomainException> adjustResult = account.AdjustBalance(
                        occurredAt: dateProvider.UtcNow,
                        sourceId: item.TransactionId,
                        sourceType: AggregateTypeNames.Transaction,
                        direction: item.Direction,
                        oldRate: item.CurrentRate,
                        newRate: newRate.Value,
                        amount: item.Amount
                    );

                    if (adjustResult.IsFailure)
                    {
                        logger.ZLogWarning(message: $"AdjustBalance failed for transaction {item.TransactionId}: {adjustResult.Error!.Message}. Skipping.");
                        return;
                    }

                    await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
                    {
                        await accountRepository.SaveAsync(account: account, ct: innerCt);
                        await transactionWriteRepository.UpdateRateAsync(transactionId: item.TransactionId, newRate: newRate.Value, ct: innerCt);
                    }, ct: innerCt);
                }, ct: ct);

                adjusted++;
                logger.ZLogInformation(message: $"Adjusted transaction {item.TransactionId}: rate {item.CurrentRate} → {newRate}.");
            }
            catch (Exception ex)
            {
                failed++;
                logger.ZLogError(exception: ex, message: $"Failed to adjust transaction {item.TransactionId}.");
            }
        }

        logger.ZLogInformation(message: $"Transactions complete. Adjusted: {adjusted}, failed: {failed}.");
    }

    private async Task ProcessTransfersAsync(CancellationToken ct)
    {
        IReadOnlyList<PendingRateTransfer> pending = await transferReadRepository.GetPendingRateAsync(ct: ct);

        if (pending.Count == 0)
        {
            logger.ZLogInformation(message: $"No pending rate transfers found.");
            return;
        }

        logger.ZLogInformation(message: $"Processing {pending.Count} pending rate transfer(s).");

        int adjusted = 0;
        int failed = 0;

        foreach (PendingRateTransfer item in pending)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                decimal? newRate = await currencyRateReadRepository.GetRateAsync(
                    baseCurrencyCode: item.CurrencyFrom,
                    targetCurrencyCode: item.CurrencyTo,
                    date: DateOnly.FromDateTime(dateTime: item.OccurredAt),
                    ct: ct
                );

                if (newRate is null)
                {
                    logger.ZLogWarning(message: $"Rate not found for transfer {item.TransferId} ({item.CurrencyFrom} → {item.CurrencyTo} on {item.OccurredAt:d}). Skipping.");
                    continue;
                }

                if (newRate == item.CurrentRate)
                {
                    await transferWriteRepository.UpdateRateAsync(transferId: item.TransferId, newRate: newRate.Value, ct: ct);
                    adjusted++;
                    continue;
                }

                await ExecuteWithRetryAsync(operation: async innerCt =>
                {
                    Account? fromAccount = await accountRepository.GetByIdAsync(accountId: item.FromAccountId, ct: innerCt);
                    Account? toAccount = await accountRepository.GetByIdAsync(accountId: item.ToAccountId, ct: innerCt);

                    if (fromAccount is null || toAccount is null)
                    {
                        logger.ZLogWarning(message: $"Account(s) not found for transfer {item.TransferId}. Skipping.");
                        return;
                    }

                    Result<Unit, DomainException> fromResult = fromAccount.AdjustBalance(
                        occurredAt: dateProvider.UtcNow,
                        sourceId: item.TransferId,
                        sourceType: AggregateTypeNames.Transfer,
                        direction: DirectionType.Debit,
                        oldRate: item.CurrentRate,
                        newRate: newRate.Value,
                        amount: item.AmountFrom
                    );

                    if (fromResult.IsFailure)
                    {
                        logger.ZLogWarning(message: $"AdjustBalance failed for from-account on transfer {item.TransferId}: {fromResult.Error!.Message}. Skipping.");
                        return;
                    }

                    Result<Unit, DomainException> toResult = toAccount.AdjustBalance(
                        occurredAt: dateProvider.UtcNow,
                        sourceId: item.TransferId,
                        sourceType: AggregateTypeNames.Transfer,
                        direction: DirectionType.Credit,
                        oldRate: item.CurrentRate,
                        newRate: newRate.Value,
                        amount: item.AmountFrom
                    );

                    if (toResult.IsFailure)
                    {
                        logger.ZLogWarning(message: $"AdjustBalance failed for to-account on transfer {item.TransferId}: {toResult.Error!.Message}. Skipping.");
                        return;
                    }

                    await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
                    {
                        await accountRepository.SaveAsync(account: fromAccount, ct: innerCt);
                        await accountRepository.SaveAsync(account: toAccount, ct: innerCt);
                        await transferWriteRepository.UpdateRateAsync(transferId: item.TransferId, newRate: newRate.Value, ct: innerCt);
                    }, ct: innerCt);
                }, ct: ct);

                adjusted++;
                logger.ZLogInformation(message: $"Adjusted transfer {item.TransferId}: rate {item.CurrentRate} → {newRate}.");
            }
            catch (Exception ex)
            {
                failed++;
                logger.ZLogError(exception: ex, message: $"Failed to adjust transfer {item.TransferId}.");
            }
        }

        logger.ZLogInformation(message: $"Transfers complete. Adjusted: {adjusted}, failed: {failed}.");
    }
}