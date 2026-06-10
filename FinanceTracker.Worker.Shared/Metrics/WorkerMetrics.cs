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

	// ── Infrastructure: Outbox ────────────────────────────────────

	public static readonly Gauge<int> OutboxPending = Meter.CreateGauge<int>(
		name: "outbox.pending",
		description: "Number of outbox messages waiting to be published."
	);

	public static readonly Counter<int> OutboxPublished = Meter.CreateCounter<int>(
		name: "outbox.published",
		description: "Total number of outbox messages successfully published."
	);

	public static readonly Counter<int> OutboxFailed = Meter.CreateCounter<int>(
		name: "outbox.failed",
		description: "Total number of outbox messages moved to dead letter."
	);

	public static readonly Gauge<int> DeadLetterCount = Meter.CreateGauge<int>(
		name: "dead_letter.count",
		description: "Current number of messages in dead letter queue."
	);

	public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>(
		name: "message.processing.duration",
		unit: "ms",
		description: "Duration of individual message processing in milliseconds."
	);

	// ── Infrastructure: Transfers ─────────────────────────────────

	public static readonly Gauge<int> TransferCreditPending = Meter.CreateGauge<int>(
		name: "transfer.credit.pending",
		description: "Number of transfers where debit was applied but credit has not been recorded yet."
	);

	public static readonly Histogram<double> TransferCreditDuration = Meter.CreateHistogram<double>(
		name: "transfer.credit.duration",
		unit: "ms",
		description: "Time elapsed between debit event and credit application in milliseconds."
	);

	// ── Business: Transactions ────────────────────────────────────

	public static readonly Counter<long> TransactionsCreated = Meter.CreateCounter<long>(
		name: "transactions.created",
		description: "Total number of transactions created. Tagged by direction (debit/credit)."
	);

	// ── Business: Transfers ───────────────────────────────────────

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

	// ── Business: Balance Adjustment ─────────────────────────────

	public static readonly Counter<long> BalanceAdjustmentAdjusted = Meter.CreateCounter<long>(
		name: "balance_adjustment.adjusted",
		description: "Total items with balance adjusted after rate update. Tagged by source_type (transaction/transfer)."
	);

	public static readonly Counter<long> BalanceAdjustmentSkipped = Meter.CreateCounter<long>(
		name: "balance_adjustment.skipped",
		description: "Total items skipped during balance adjustment. Tagged by source_type."
	);

	public static readonly Counter<long> BalanceAdjustmentFailed = Meter.CreateCounter<long>(
		name: "balance_adjustment.failed",
		description: "Total items that failed during balance adjustment. Tagged by source_type."
	);

	// ── Business: Currency Rates ──────────────────────────────────

	public static readonly Counter<long> CurrencyRatesUpserted = Meter.CreateCounter<long>(
		name: "currency_rates.upserted",
		description: "Total number of currency rates upserted."
	);

	public static readonly Counter<long> CurrencyRatesFetchFailed = Meter.CreateCounter<long>(
		name: "currency_rates.fetch_failed",
		description: "Total number of currency fetch failures by base currency."
	);
}