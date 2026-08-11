using System.Diagnostics.Metrics;

namespace FinanceTracker.Worker.Shared.Metrics;

/// <summary>
/// OpenTelemetry metrics emitted by worker services.
/// All instruments are registered under the <c>FinanceTracker.Workers</c> meter.
/// Expose via <c>AddMeter("FinanceTracker.Workers")</c> in your OTEL configuration.
/// </summary>
public static class WorkerMetrics
{
	public const string MeterName = "FinanceTracker.Workers";

	private static readonly Meter Meter = new Meter(name: MeterName);

	// All jobs

	public static readonly Counter<long> JobExecutionFailed = Meter.CreateCounter<long>(
		name: "job.execution.failed",
		description: "Total number of job executions that threw an unhandled exception. Tagged by job (job type name)."
	);

	// Outbox

	/// <remarks>
	/// Must be the full pending backlog, not the size of the batch the publisher just claimed
	/// </remarks>
	public static readonly Gauge<int> OutboxPending = Meter.CreateGauge<int>(
		name: "outbox.pending",
		description: "Number of outbox messages waiting to be published, across the whole table."
	);

	public static readonly Counter<int> OutboxPublished = Meter.CreateCounter<int>(
		name: "outbox.published",
		description: "Total number of outbox messages successfully published."
	);

	public static readonly Counter<int> OutboxFailed = Meter.CreateCounter<int>(
		name: "outbox.failed",
		description: "Total number of outbox messages moved to dead letter."
	);

	/// <summary>
	/// Events escalated for manual resolution and still unresolved
	/// </summary>
	public static readonly Gauge<int> UnresolvableEventsPending = Meter.CreateGauge<int>(
		name: "unresolvable_events.pending",
		description: "Number of escalated events still awaiting manual resolution."
	);

	public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>(
		name: "message.processing.duration",
		unit: "ms",
		description: "Duration of individual message processing in milliseconds."
	);

	// Transfers

	public static readonly Gauge<int> TransferCreditPending = Meter.CreateGauge<int>(
		name: "transfer.credit.pending",
		description: "Number of transfers where debit was applied but credit has not been recorded yet."
	);

	public static readonly Histogram<double> TransferCreditDuration = Meter.CreateHistogram<double>(
		name: "transfer.credit.duration",
		unit: "ms",
		description: "Time elapsed between debit event and credit application in milliseconds."
	);

	// Transactions

	public static readonly Counter<long> TransactionsCreated = Meter.CreateCounter<long>(
		name: "transactions.created",
		description: "Total number of transactions created. Tagged by direction (debit/credit)."
	);

	// Recurring transactions

	public static readonly Counter<long> RecurringTransactionsFailed = Meter.CreateCounter<long>(
		name: "recurring_transactions.failed",
		description: "Total number of recurring transaction occurrences that could not be turned into an actual transaction and were escalated to unresolvable_events."
	);

	// Transfers

	public static readonly Counter<long> TransfersCompleted = Meter.CreateCounter<long>(
		name: "transfers.completed",
		description: "Total number of transfers successfully completed (debit + credit applied)."
	);

	public static readonly Counter<long> TransfersCompensated = Meter.CreateCounter<long>(
		name: "transfers.compensated",
		description: "Total number of transfers refunded due to credit failure."
	);

	public static readonly Counter<long> TransfersFailed = Meter.CreateCounter<long>(
		name: "transfers.failed",
		description: "Total number of transfers that failed and require manual resolution."
	);

	// Balance adjustment

	public static readonly Counter<long> BalanceAdjustmentResolved = Meter.CreateCounter<long>(
		name: "balance_adjustment_resolved",
		description: "Operations whose pending rate was settled against the real rate."
	);

	public static readonly Counter<long> BalanceAdjustmentApproximated = Meter.CreateCounter<long>(
		name: "balance_adjustment_approximated",
		description: "Operations whose pending rate was written off as approximate after the grace period."
	);

	public static readonly Counter<long> BalanceAdjustmentUnresolvable = Meter.CreateCounter<long>(
		name: "balance_adjustment_unresolvable",
		description: "Operations whose rate correction was rejected and escalated for manual resolution."
	);

	public static readonly Counter<long> BalanceAdjustmentFailed = Meter.CreateCounter<long>(
		name: "balance_adjustment.failed",
		description: "Total items that failed during balance adjustment. Tagged by source_type."
	);

	// Currency rates

	public static readonly Counter<long> CurrencyRatesUpserted = Meter.CreateCounter<long>(
		name: "currency_rates.upserted",
		description: "Total number of currency rates upserted."
	);

	public static readonly Counter<long> CurrencyRatesFetchFailed = Meter.CreateCounter<long>(
		name: "currency_rates.fetch_failed",
		description: "Total number of currency fetch failures by base currency."
	);

	public static readonly Counter<long> CurrencyRatesNormalized = Meter.CreateCounter<long>(
		name: "financetracker.currency_rates.normalized",
		unit: "{rate}",
		description: "Exchange rates rounded to the stored scale on ingestion."
	);

	public static readonly Counter<long> CurrencyRatesRejected = Meter.CreateCounter<long>(
		name: "financetracker.currency_rates.rejected",
		unit: "{rate}",
		description: "Exchange rates discarded on ingestion because the provider's value was not usable."
	);
}
