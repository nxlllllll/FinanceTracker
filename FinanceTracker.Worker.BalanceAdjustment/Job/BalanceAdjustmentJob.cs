using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.Pending;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Utilities.Retry;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Worker.BalanceAdjustment.Job;

[DisallowConcurrentExecution]
public sealed class BalanceAdjustmentJob(
	ITransactionReadRepository transactionReadRepository,
	ITransactionRepository transactionRepository,
	ITransactionWriteRepository transactionWriteRepository,
	ITransferReadRepository transferReadRepository,
	ITransferRepository transferRepository,
	ITransferWriteRepository transferWriteRepository,
	IAccountRepository accountRepository,
	ICurrencyRateReadRepository currencyRateReadRepository,
	IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	IOptionsMonitor<BalanceAdjustmentJobOptions> options,
	ILogger<BalanceAdjustmentJob> logger
) : BaseJob<BalanceAdjustmentJobOptions>(options: options, logger: logger)
{
	private sealed record Outcome(AdjustResult Result, string? Reason = null);

	private sealed class Tally
	{
		public int Resolved { get; set; }
		public int Approximated { get; set; }
		public int Unresolvable { get; set; }
		public int Waiting { get; set; }
		public int Failed { get; set; }
	}

	protected override async Task ProcessAsync(BalanceAdjustmentJobOptions options, CancellationToken ct)
	{
		await ProcessTransactionsAsync(options: options, ct: ct);
		await ProcessTransfersAsync(options: options, ct: ct);
	}

	private async Task ProcessTransactionsAsync(BalanceAdjustmentJobOptions options, CancellationToken ct)
	{
		Dictionary<Guid, Account> accountCache = new Dictionary<Guid, Account>();
		Tally tally = new Tally();

		DateTimeOffset? cursorOccurredAt = null;
		Guid? cursorId = null;

		while (!ct.IsCancellationRequested)
		{
			IReadOnlyList<PendingRateTransaction> page = await transactionReadRepository.GetPendingRateAsync(
				batchSize: options.BatchSize,
				cursorOccurredAt: cursorOccurredAt,
				cursorId: cursorId,
				ct: ct
			);

			if (page.Count == 0)
				break;

			foreach (PendingRateTransaction item in page)
			{
				if (ct.IsCancellationRequested)
					break;

				Outcome outcome = await RunWithRetryAsync(
					itemId: item.TransactionId,
					accountCache: accountCache,
					options: options,
					work: innerCt => SettleTransactionAsync(item: item, accountCache: accountCache, options: options, ct: innerCt),
					ct: ct
				);

				Record(tally: tally, outcome: outcome, sourceType: AggregateTypeNames.Transaction, itemId: item.TransactionId);
			}

			cursorOccurredAt = page[^1].OccurredAt;
			cursorId = page[^1].TransactionId;

			if (page.Count < options.BatchSize)
				break;
		}

		LogSummary(entityName: AggregateTypeNames.Transaction, tally: tally);
	}

	private async Task<Outcome> SettleTransactionAsync(
		PendingRateTransaction item,
		Dictionary<Guid, Account> accountCache,
		BalanceAdjustmentJobOptions options,
		CancellationToken ct)
	{
		decimal? newRate = await currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: item.TransactionCurrency,
			targetCurrencyCode: item.BaseCurrency,
			date: DateOnly.FromDateTime(dateTime: item.OccurredAt.UtcDateTime),
			ct: ct
		);

		if (newRate is null && !HasOutlivedGrace(rateStatusChangedAt: item.RateStatusChangedAt, options: options))
			return new Outcome(Result: AdjustResult.Waiting);

		Transaction? transaction = await transactionRepository.GetByIdAsync(
			transactionId: item.TransactionId,
			userId: item.UserId,
			ct: ct
		);

		if (transaction is null)
			return new Outcome(Result: AdjustResult.Waiting, Reason: "Transaction disappeared between queue and settlement.");

		if (!transaction.RateStatus.IsOpen())
			return new Outcome(Result: AdjustResult.Waiting, Reason: $"Rate already settled as {transaction.RateStatus}.");

		if (newRate is null)
		{
			return await CloseAsync(
				apply: () => transaction.ApproximateRate(changedAt: dateProvider.UtcNow),
				save: innerCt => transactionWriteRepository.SaveRateResolutionAsync(transaction: transaction, ct: innerCt),
				result: AdjustResult.Approximated,
				reason: $"No rate for {item.TransactionCurrency} -> {item.BaseCurrency} on {item.OccurredAt:d} after {options.RateGracePeriodDays} day(s).",
				ct: ct
			);
		}

		Account? account = await GetOrLoadAccountAsync(cache: accountCache, accountId: transaction.AccountId, ct: ct);
		if (account is null)
		{
			return await EscalateAsync(
				apply: () => transaction.MarkRateUnresolvable(changedAt: dateProvider.UtcNow),
				save: innerCt => transactionWriteRepository.SaveRateResolutionAsync(transaction: transaction, ct: innerCt),
				referenceId: item.TransactionId,
				sourceType: AggregateTypeNames.Transaction,
				reason: $"Account {transaction.AccountId} not found.",
				payload: new { transactionId = item.TransactionId, accountId = transaction.AccountId },
				ct: ct
			);
		}

		Result<Unit, DomainException> adjusted = account.AdjustBalance(
			occurredAt: dateProvider.UtcNow,
			sourceId: transaction.Id,
			sourceType: AggregateTypeNames.Transaction,
			direction: transaction.Direction,
			oldRate: transaction.ExchangeRate,
			newRate: newRate.Value,
			amount: transaction.Amount.Amount
		);

		if (adjusted.IsFailure)
		{
			return await EscalateAsync(
				apply: () => transaction.MarkRateUnresolvable(changedAt: dateProvider.UtcNow),
				save: innerCt => transactionWriteRepository.SaveRateResolutionAsync(transaction: transaction, ct: innerCt),
				referenceId: item.TransactionId,
				sourceType: AggregateTypeNames.Transaction,
				reason: adjusted.Error!.Message,
				payload: new { transactionId = item.TransactionId, accountId = account.Id, oldRate = transaction.ExchangeRate, newRate = newRate.Value },
				ct: ct
			);
		}

		Result<Unit, DomainException> resolved = transaction.ResolveRate(newRate: newRate.Value, changedAt: dateProvider.UtcNow);
		if (resolved.IsFailure)
		{
			accountCache.Remove(key: account.Id);

			return await EscalateAsync(
				apply: () => transaction.MarkRateUnresolvable(changedAt: dateProvider.UtcNow),
				save: innerCt => transactionWriteRepository.SaveRateResolutionAsync(transaction: transaction, ct: innerCt),
				referenceId: item.TransactionId,
				sourceType: AggregateTypeNames.Transaction,
				reason: resolved.Error!.Message,
				payload: new { transactionId = item.TransactionId, newRate = newRate.Value },
				ct: ct
			);
		}

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await accountRepository.SaveAsync(account: account, ct: ct);
			await transactionWriteRepository.SaveRateResolutionAsync(transaction: transaction, ct: ct);
		}, ct: ct);

		return new Outcome(Result: AdjustResult.Resolved);
	}

	private async Task ProcessTransfersAsync(BalanceAdjustmentJobOptions options, CancellationToken ct)
	{
		Dictionary<Guid, Account> accountCache = new Dictionary<Guid, Account>();
		Tally tally = new Tally();

		DateTimeOffset? cursorOccurredAt = null;
		Guid? cursorId = null;

		while (!ct.IsCancellationRequested)
		{
			IReadOnlyList<PendingRateTransfer> page = await transferReadRepository.GetPendingRateAsync(
				batchSize: options.BatchSize,
				cursorOccurredAt: cursorOccurredAt,
				cursorId: cursorId,
				ct: ct
			);

			if (page.Count == 0)
				break;

			foreach (PendingRateTransfer item in page)
			{
				if (ct.IsCancellationRequested)
					break;

				Outcome outcome = await RunWithRetryAsync(
					itemId: item.TransferId,
					accountCache: accountCache,
					options: options,
					work: innerCt => SettleTransferAsync(item: item, accountCache: accountCache, options: options, ct: innerCt),
					ct: ct
				);

				Record(tally: tally, outcome: outcome, sourceType: AggregateTypeNames.Transfer, itemId: item.TransferId);
			}

			cursorOccurredAt = page[^1].OccurredAt;
			cursorId = page[^1].TransferId;

			if (page.Count < options.BatchSize)
				break;
		}

		LogSummary(entityName: AggregateTypeNames.Transfer, tally: tally);
	}

	private async Task<Outcome> SettleTransferAsync(
		PendingRateTransfer item,
		Dictionary<Guid, Account> accountCache,
		BalanceAdjustmentJobOptions options,
		CancellationToken ct)
	{
		decimal? newRate = await currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: item.CurrencyFrom,
			targetCurrencyCode: item.CurrencyTo,
			date: DateOnly.FromDateTime(dateTime: item.OccurredAt.UtcDateTime),
			ct: ct
		);

		if (newRate is null && !HasOutlivedGrace(rateStatusChangedAt: item.RateStatusChangedAt, options: options))
			return new Outcome(Result: AdjustResult.Waiting);

		Transfer? transfer = await transferRepository.GetByIdAsync(transferId: item.TransferId, ct: ct);

		if (transfer is null)
			return new Outcome(Result: AdjustResult.Waiting, Reason: "Transfer disappeared between queue and settlement.");

		if (!transfer.RateStatus.IsOpen())
			return new Outcome(Result: AdjustResult.Waiting, Reason: $"Rate already settled as {transfer.RateStatus}.");

		if (newRate is null)
		{
			return await CloseAsync(
				apply: () => transfer.ApproximateRate(changedAt: dateProvider.UtcNow),
				save: innerCt => transferWriteRepository.SaveRateResolutionAsync(transfer: transfer, ct: innerCt),
				result: AdjustResult.Approximated,
				reason: $"No rate for {item.CurrencyFrom} > {item.CurrencyTo} on {item.OccurredAt:d} after {options.RateGracePeriodDays} day(s).",
				ct: ct
			);
		}

		Account? toAccount = await GetOrLoadAccountAsync(cache: accountCache, accountId: transfer.ToAccountId, ct: ct);
		if (toAccount is null)
		{
			return await EscalateAsync(
				apply: () => transfer.MarkRateUnresolvable(changedAt: dateProvider.UtcNow),
				save: innerCt => transferWriteRepository.SaveRateResolutionAsync(transfer: transfer, ct: innerCt),
				referenceId: item.TransferId,
				sourceType: AggregateTypeNames.Transfer,
				reason: $"toAccount {transfer.ToAccountId} not found.",
				payload: new { transferId = item.TransferId, toAccountId = transfer.ToAccountId },
				ct: ct
			);
		}

		Result<Unit, DomainException> adjusted = toAccount.AdjustBalance(
			occurredAt: dateProvider.UtcNow,
			sourceId: transfer.Id,
			sourceType: AggregateTypeNames.Transfer,
			direction: DirectionType.Credit,
			oldRate: transfer.ExchangeRate,
			newRate: newRate.Value,
			amount: transfer.AmountFrom.Amount
		);

		if (adjusted.IsFailure)
		{
			return await EscalateAsync(
				apply: () => transfer.MarkRateUnresolvable(changedAt: dateProvider.UtcNow),
				save: innerCt => transferWriteRepository.SaveRateResolutionAsync(transfer: transfer, ct: innerCt),
				referenceId: item.TransferId,
				sourceType: AggregateTypeNames.Transfer,
				reason: adjusted.Error!.Message,
				payload: new { transferId = item.TransferId, toAccountId = toAccount.Id, oldRate = transfer.ExchangeRate, newRate = newRate.Value },
				ct: ct
			);
		}

		Result<Unit, DomainException> resolved = transfer.ResolveRate(newRate: newRate.Value, changedAt: dateProvider.UtcNow);
		if (resolved.IsFailure)
		{
			accountCache.Remove(key: toAccount.Id);

			return await EscalateAsync(
				apply: () => transfer.MarkRateUnresolvable(changedAt: dateProvider.UtcNow),
				save: innerCt => transferWriteRepository.SaveRateResolutionAsync(transfer: transfer, ct: innerCt),
				referenceId: item.TransferId,
				sourceType: AggregateTypeNames.Transfer,
				reason: resolved.Error!.Message,
				payload: new { transferId = item.TransferId, amountFrom = transfer.AmountFrom.Amount, newRate = newRate.Value },
				ct: ct
			);
		}

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await accountRepository.SaveAsync(account: toAccount, ct: ct);
			await transferWriteRepository.SaveRateResolutionAsync(transfer: transfer, ct: ct);
		}, ct: ct);

		return new Outcome(Result: AdjustResult.Resolved);
	}

	/// <summary>Closes a row without touching any balance — the placeholder rate stands as final.</summary>
	private async Task<Outcome> CloseAsync(
		Func<Result<Unit, DomainException>> apply,
		Func<CancellationToken, Task> save,
		AdjustResult result,
		string reason,
		CancellationToken ct)
	{
		Result<Unit, DomainException> applied = apply();
		if (applied.IsFailure)
			return new Outcome(Result: AdjustResult.Failed, Reason: applied.Error!.Message);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () => await save(ct), ct: ct);

		return new Outcome(Result: result, Reason: reason);
	}

	/// <summary>
	/// Closes a row as <c>Unresolvable</c> and records why, in the same transaction. The two must land
	/// together: a row closed without a trail is a balance that is knowingly wrong and nobody knows it.
	/// </summary>
	private async Task<Outcome> EscalateAsync(
		Func<Result<Unit, DomainException>> apply,
		Func<CancellationToken, Task> save,
		Guid referenceId,
		string sourceType,
		string reason,
		object payload,
		CancellationToken ct)
	{
		Result<Unit, DomainException> applied = apply();
		if (applied.IsFailure)
			return new Outcome(Result: AdjustResult.Failed, Reason: applied.Error!.Message);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await unresolvableEventWriteRepository.CreateAsync(
				type: UnresolvableEventType.RateAdjustmentFailed,
				referenceId: referenceId,
				reason: reason,
				payload: JsonSerializer.Serialize(value: payload),
				occurredAt: dateProvider.UtcNow,
				ct: ct
			);

			await save(ct);
		}, ct: ct);

		return new Outcome(Result: AdjustResult.Unresolvable, Reason: reason);
	}

	private bool HasOutlivedGrace(DateTimeOffset rateStatusChangedAt, BalanceAdjustmentJobOptions options)
		=> dateProvider.UtcNow - rateStatusChangedAt > TimeSpan.FromDays(value: options.RateGracePeriodDays);

	private async Task<Outcome> RunWithRetryAsync(
		Guid itemId,
		Dictionary<Guid, Account> accountCache,
		BalanceAdjustmentJobOptions options,
		Func<CancellationToken, Task<Outcome>> work,
		CancellationToken ct)
	{
		try
		{
			return await RetryDelayCalculator.ExecuteWithRetryAsync(
				operation: work,
				logging: (exception, attempt, delay) =>
				{
					accountCache.Clear();

					logger.ZLogWarning(exception: exception, message: $"[ConcurrencyRetry] {itemId}: attempt {attempt + 1}/{options.MaxRetries} failed. Retrying in {delay}ms.");
				},
				maxRetries: options.MaxRetries,
				baseDelayMs: options.BaseDelayMs,
				useJitter: options.UseJitter,
				ct: ct
			);
		}
		catch (Exception ex)
		{
			accountCache.Clear();
			logger.ZLogError(exception: ex, message: $"Failed to settle {itemId}. It stays pending and will be retried on the next run.");
			return new Outcome(Result: AdjustResult.Failed, Reason: ex.Message);
		}
	}

	private async Task<Account?> GetOrLoadAccountAsync(
		Dictionary<Guid, Account> cache,
		Guid accountId,
		CancellationToken ct)
	{
		if (cache.TryGetValue(key: accountId, out Account? cached) && cached.Events.Count == 0)
			return cached;

		Account? loaded = await accountRepository.GetByIdAsync(accountId: accountId, ct: ct);

		if (loaded is not null)
			cache[accountId] = loaded;
		else
			cache.Remove(key: accountId);

		return loaded;
	}

	private void Record(Tally tally, Outcome outcome, string sourceType, Guid itemId)
	{
		KeyValuePair<string, object?> tag = new KeyValuePair<string, object?>(key: "source_type", value: sourceType.ToLowerInvariant());

		switch (outcome.Result)
		{
			case AdjustResult.Resolved:
				tally.Resolved++;
				WorkerMetrics.BalanceAdjustmentResolved.Add(delta: 1, tag);
				break;

			case AdjustResult.Approximated:
				tally.Approximated++;
				WorkerMetrics.BalanceAdjustmentApproximated.Add(delta: 1, tag);
				logger.ZLogWarning(message: $"[{sourceType}] {itemId}: rate written off as approximate. {outcome.Reason}");
				break;

			case AdjustResult.Unresolvable:
				tally.Unresolvable++;
				WorkerMetrics.BalanceAdjustmentUnresolvable.Add(delta: 1, tag);
				logger.ZLogError(message: $"[{sourceType}] {itemId}: rate settled but correction rejected — escalated to unresolvable_events. {outcome.Reason}");
				break;

			case AdjustResult.Waiting:
				tally.Waiting++;
				break;

			case AdjustResult.Failed:
				tally.Failed++;
				WorkerMetrics.BalanceAdjustmentFailed.Add(delta: 1, tag);
				break;
		}
	}

	private void LogSummary(string entityName, Tally tally)
	{
		logger.ZLogInformation(message: $"""
			{entityName}s settled. Resolved: {tally.Resolved}, approximated: {tally.Approximated},
			unresolvable: {tally.Unresolvable}, still waiting: {tally.Waiting}, failed: {tally.Failed}.
		""");
	}
}
