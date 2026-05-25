using System.Diagnostics.Metrics;

namespace FinanceTracker.Worker.Shared.Metrics;

public static class WorkerMetrics
{
	public const string MeterName = "FinanceTracker.Workers";

	private static readonly Meter Meter = new Meter(name: MeterName);

	// outbox.pending — сколько сообщений ждут отправки
	public static readonly Gauge<int> OutboxPending = Meter.CreateGauge<int>(
		name: "outbox.pending",
		description: "Number of outbox messages waiting to be published."
	);

	// outbox.published — счётчик успешно опубликованных
	public static readonly Counter<int> OutboxPublished = Meter.CreateCounter<int>(
		name: "outbox.published",
		description: "Total number of outbox messages successfully published."
	);

	// outbox.failed — счётчик перешедших в dead letter
	public static readonly Counter<int> OutboxFailed = Meter.CreateCounter<int>(
		name: "outbox.failed",
		description: "Total number of outbox messages moved to dead letter."
	);

	// dead_letter.count — текущее количество в dead letter
	public static readonly Gauge<int> DeadLetterCount = Meter.CreateGauge<int>(
		name: "dead_letter.count",
		description: "Current number of messages in dead letter queue."
	);

	// message.processing.duration — гистограмма времени обработки
	public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>(
		name: "message.processing.duration",
		unit: "ms",
		description: "Duration of individual message processing in milliseconds."
	);

	// transfer.credit.pending — сколько трансферов без кредита
	// Eventual consistency: дебет и кредит применяются в разных транзакциях.
	// Если это значение > 0 дольше нескольких минут — кредитный воркер завис или упал.
	public static readonly Gauge<int> TransferCreditPending = Meter.CreateGauge<int>(
		name: "transfer.credit.pending",
		description: "Number of transfers where debit was applied but credit has not been recorded yet. Values above 0 for more than a few minutes indicate a stuck or failed transfer projection worker."
	);

	// transfer.credit.duration — время от дебета до применения кредита
	public static readonly Histogram<double> TransferCreditDuration = Meter.CreateHistogram<double>(
		name: "transfer.credit.duration",
		unit: "ms",
		description: "Time elapsed between debit event and credit application in milliseconds."
	);
}