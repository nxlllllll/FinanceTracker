using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Observability.Tracing;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.Outbox.Job;

/// <summary>
/// Guarantees at-least-once delivery: RabbitMQ publish is not transactional with PostgreSQL,
/// so a message may be published but not marked as processed if the worker crashes after publish.
/// Consumers must be idempotent — duplicates are handled via the processed_messages table.
/// </summary>
[DisallowConcurrentExecution]
public sealed class OutboxPublisherJob(
	IOutboxReadRepository outboxReadRepository,
	IOutboxWriteRepository outboxWriteRepository,
	IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IRabbitMqPublisher publisher,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	IOptionsMonitor<OutboxOptions> options,
	ILogger<OutboxPublisherJob> logger
) : BaseJob<OutboxOptions>(options: options, logger: logger)
{
	private sealed record PublishOutcome(PendingOutboxMessage Message, Exception? Failure, bool Cancelled)
	{
		public bool IsPublished => Failure is null && !Cancelled;
	}

	protected override async Task ProcessAsync(OutboxOptions options, CancellationToken ct)
	{
		IReadOnlyList<PendingOutboxMessage> messages = await outboxReadRepository.ClaimPendingBatchAsync(
			batchSize: options.BatchSize,
			now: dateProvider.UtcNow,
			leaseDuration: TimeSpan.FromSeconds(value: options.LeaseDurationSeconds),
			ct: ct
		);

		if (messages.Count > 0)
			await PublishBatchAsync(messages: messages, options: options, ct: ct);

		if (ct.IsCancellationRequested)
			return;

		int stillPending = await outboxReadRepository.CountPendingAsync(ct: ct);
		WorkerMetrics.OutboxPending.Record(value: stillPending);
	}

	private async Task PublishBatchAsync(
		IReadOnlyList<PendingOutboxMessage> messages,
		OutboxOptions options,
		CancellationToken ct)
	{
		logger.ZLogInformation(message: $"Publishing {messages.Count} outbox message(s) with a concurrency of {options.PublishConcurrency}.");

		PublishOutcome[] outcomes = await PublishAllAsync(messages: messages, options: options, ct: ct);

		await SettleAsync(outcomes: outcomes, options: options, ct: ct);
	}

	private async Task<PublishOutcome[]> PublishAllAsync(
		IReadOnlyList<PendingOutboxMessage> messages,
		OutboxOptions options,
		CancellationToken ct)
	{
		PublishOutcome[] outcomes = new PublishOutcome[messages.Count];

		await Parallel.ForEachAsync(
			source: Enumerable.Range(start: 0, count: messages.Count),
			parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = options.PublishConcurrency },
			body: async (index, _) => outcomes[index] = await PublishOneAsync(message: messages[index], ct: ct)
		);

		return outcomes;
	}

	private async Task<PublishOutcome> PublishOneAsync(PendingOutboxMessage message, CancellationToken ct)
	{
		if (ct.IsCancellationRequested)
			return new PublishOutcome(Message: message, Failure: null, Cancelled: true);

		Stopwatch sw = Stopwatch.StartNew();
		Activity? activity = null;

		try
		{
			OutboxPayload payload = JsonSerializer.Deserialize<OutboxPayload>(json: message.Payload)
				?? throw new SerializationException(message: "Failed to deserialize outbox payload.");

			activity = StartPublishActivity(message: message, payload: payload);

			AggregateEventsMessage brokerMessage = new AggregateEventsMessage(
				MessageId: message.Id,
				AggregateId: message.AggregateId,
				AggregateType: message.AggregateType,
				CorrelationId: payload.CorrelationId,
				Events: payload.Events.Select(selector: e => new EventEnvelope(
					EventType: e.EventType,
					EventPayload: e.EventPayload
				)).ToList()
			);

			await publisher.PublishAsync(message: brokerMessage, correlationId: payload.CorrelationId, ct: ct);

			activity?.SetStatus(code: ActivityStatusCode.Ok);
			WorkerMetrics.OutboxPublished.Add(delta: 1);

			return new PublishOutcome(Message: message, Failure: null, Cancelled: false);
		}
		catch (Exception exception)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: exception.Message);

			if (ct.IsCancellationRequested)
				return new PublishOutcome(Message: message, Failure: null, Cancelled: true);

			logger.ZLogError(exception: exception, message: $"Failed to publish outbox message {message.Id}.");
			return new PublishOutcome(Message: message, Failure: exception, Cancelled: false);
		}
		finally
		{
			activity?.Dispose();
			WorkerMetrics.MessageProcessingDuration.Record(value: sw.Elapsed.TotalMilliseconds);
		}
	}

	private async Task SettleAsync(PublishOutcome[] outcomes, OutboxOptions options, CancellationToken ct)
	{
		Guid[] publishedIds = outcomes.Where(predicate: o => o.IsPublished).Select(selector: o => o.Message.Id).ToArray();

		if (publishedIds.Length > 0)
		{
			await outboxWriteRepository.MarkAsPublishedBatchAsync(
				messageIds: publishedIds,
				processedAt: dateProvider.UtcNow,
				ct: ct.IsCancellationRequested ? CancellationToken.None : ct
			);

			logger.ZLogInformation(message: $"Published {publishedIds.Length}/{outcomes.Length} outbox message(s).");
		}

		if (ct.IsCancellationRequested)
			return;

		foreach (PublishOutcome outcome in outcomes.Where(predicate: o => o.Failure is not null))
			await UpdateRetryStateAsync(message: outcome.Message, options: options, ct: ct);
	}

	/// <summary>
	/// Opens a producer span attributed to the request that wrote the row, when that context survived.
	/// </summary>
	private static Activity? StartPublishActivity(PendingOutboxMessage message, OutboxPayload payload)
	{
		Activity? activity = FinanceTrackerActivitySource.Instance.StartActivity(
			name: FinanceTrackerActivitySource.Operations.OutboxPublish,
			kind: ActivityKind.Producer,
			FinanceTrackerActivitySource.ParseTraceParent(
				traceParent: payload.TraceParent,
				traceState: payload.TraceState
			)
		);

		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.AggregateId, value: message.AggregateId);
		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.AggregateType, value: message.AggregateType);
		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.EventsCount, value: payload.Events.Count);
		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.CorrelationId, value: payload.CorrelationId);

		return activity;
	}

	private async Task UpdateRetryStateAsync(PendingOutboxMessage message, OutboxOptions options, CancellationToken ct)
	{
		try
		{
			int newRetryCount = message.RetryCount + 1;
			DateTimeOffset? failedAt = newRetryCount >= options.MaxRetries ? dateProvider.UtcNow : null;

			await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				if (failedAt is not null)
				{
					WorkerMetrics.OutboxFailed.Add(delta: 1);
					logger.ZLogError(message: $"Outbox message {message.Id} exceeded max retries ({options.MaxRetries}). Moving to unresolvable events.");

					string payload = JsonSerializer.Serialize(value: new
					{
						aggregateId = message.AggregateId,
						aggregateType = message.AggregateType,
						retryCount = newRetryCount
					});

					await unresolvableEventWriteRepository.CreateAsync(
						type: UnresolvableEventType.OutboxDeadLetter,
						referenceId: message.Id,
						reason: $"Max retries ({options.MaxRetries}) exceeded.",
						payload: payload,
						occurredAt: dateProvider.UtcNow,
						ct: ct
					);
				}

				await outboxWriteRepository.MarkAsFailedAsync(
					messageId: message.Id,
					retryCount: newRetryCount,
					failedAt: failedAt,
					ct: ct
				);
			}, ct: ct);
		}
		catch (Exception innerException)
		{
			logger.ZLogError(exception: innerException, message: $"Failed to update retry state for outbox message {message.Id}.");
		}
	}
}
