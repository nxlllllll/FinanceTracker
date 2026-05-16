using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Jobs.Outbox;
using FinanceTracker.Worker.Shared.Metrics;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.Outbox.Jobs;

[DisallowConcurrentExecution]
public sealed class OutboxPublisherJob(
	IOutboxReadRepository outboxReadRepository,
	IOutboxWriteRepository outboxWriteRepository,
	IRabbitMqPublisher publisher,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	IOptions<OutboxOptions> outboxOptions,
	ILogger<OutboxPublisherJob> logger
) : IJob
{
	private readonly OutboxOptions _outboxOptions = outboxOptions.Value;

	public async Task Execute(IJobExecutionContext executionContext)
		=> await ProcessBatchAsync(ct: executionContext.CancellationToken);

	private async Task ProcessBatchAsync(CancellationToken ct)
	{
		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			IReadOnlyList<PendingOutboxMessage> messages = await outboxReadRepository.GetPendingBatchAsync(
				batchSize: _outboxOptions.BatchSize,
				ct: ct
			);

			WorkerMetrics.OutboxPending.Record(value: messages.Count);
			
			if (messages.Count == 0)
				return;

			logger.ZLogInformation(message: $"Publishing {messages.Count} outbox message(s).");

			int published = 0;
			foreach (PendingOutboxMessage message in messages)
			{
				Stopwatch sw = Stopwatch.StartNew();
				try
				{
					await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
					{
						await PublishMessageAsync(message: message, ct: ct);
						WorkerMetrics.OutboxPublished.Add(delta: 1);
						logger.ZLogInformation(message: $"Published: {++published}/{messages.Count}.");
					}, ct: ct);
				}
				catch (Exception exception)
				{
					if (ct.IsCancellationRequested)
						return;

					logger.ZLogError(exception: exception, message: $"Failed to publish outbox message {message.Id}.");

					await UpdateRetryStateAsync(message: message, ct: ct);
				}
				finally
				{
					WorkerMetrics.MessageProcessingDuration.Record(value: sw.Elapsed.TotalMilliseconds);
				}
			}
		}, onError: async exception => logger.ZLogError(exception: exception, message: $"Outbox batch publishing failed."), ct: ct);
	}

	private async Task PublishMessageAsync(PendingOutboxMessage message, CancellationToken ct)
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
		await outboxWriteRepository.MarkAsPublishedAsync(messageId: message.Id, processedAt: dateProvider.UtcNow, ct: ct);
	}

	private async Task UpdateRetryStateAsync(PendingOutboxMessage message, CancellationToken ct)
	{
		try
		{
			await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				int newRetryCount = message.RetryCount + 1;
				DateTime? failedAt = newRetryCount >= _outboxOptions.MaxRetries ? dateProvider.UtcNow : null;

				if (failedAt is not null)
				{
					WorkerMetrics.OutboxFailed.Add(delta: 1);
					logger.ZLogError(message: $"Outbox message {message.Id} moved to dead letter after {_outboxOptions.MaxRetries} retries.");
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