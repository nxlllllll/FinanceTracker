using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
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
	protected override async Task ProcessAsync(OutboxOptions options, CancellationToken ct)
	{
		IReadOnlyList<PendingOutboxMessage> messages = await outboxReadRepository.ClaimPendingBatchAsync(
			batchSize: options.BatchSize,
			now: dateProvider.UtcNow,
			leaseDuration: TimeSpan.FromSeconds(value: options.LeaseDurationSeconds),
			ct: ct
		);

		WorkerMetrics.OutboxPending.Record(value: messages.Count);

		if (messages.Count == 0)
			return;

		logger.ZLogInformation(message: $"Publishing {messages.Count} outbox message(s).");

		int published = 0;
		foreach (PendingOutboxMessage message in messages)
		{
			if (ct.IsCancellationRequested)
				break;

			Stopwatch sw = Stopwatch.StartNew();
			try
			{
				OutboxPayload payload = JsonSerializer.Deserialize<OutboxPayload>(json: message.Payload)
					?? throw new SerializationException(message: "Failed to deserialize outbox payload.");

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

				await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
					await outboxWriteRepository.MarkAsPublishedAsync(
						messageId: message.Id,
						processedAt: dateProvider.UtcNow,
						ct: ct
					), ct: ct);

				WorkerMetrics.OutboxPublished.Add(delta: 1);
				logger.ZLogInformation(message: $"Published: {++published}/{messages.Count}.");
			}
			catch (Exception exception)
			{
				if (ct.IsCancellationRequested)
					return;

				logger.ZLogError(exception: exception, message: $"Failed to publish outbox message {message.Id}.");
				await UpdateRetryStateAsync(message: message, options: options, ct: ct);
			}
			finally
			{
				WorkerMetrics.MessageProcessingDuration.Record(value: sw.Elapsed.TotalMilliseconds);
			}
		}
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
