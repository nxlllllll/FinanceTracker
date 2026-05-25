using System.Diagnostics;
using FinanceTracker.Contracts.Messages.Domain;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.DomainEventOutbox;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Worker.Shared.Metrics;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.DomainEventOutbox.Jobs;

[DisallowConcurrentExecution]
public sealed class DomainEventOutboxPublisherJob(
	IDomainEventOutboxReadRepository readRepository,
	IDomainEventOutboxWriteRepository writeRepository,
	IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IRabbitMqPublisher publisher,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	IOptionsMonitor<DomainEventOutboxPublisherJobOptions> options,
	ILogger<DomainEventOutboxPublisherJob> logger
) : IJob
{
	public async Task Execute(IJobExecutionContext executionContext)
	{
		DomainEventOutboxPublisherJobOptions currentOptions = options.CurrentValue;

		if (!currentOptions.IsEnabled)
		{
			logger.ZLogInformation(message: $"[{nameof(DomainEventOutboxPublisherJob)}] Disabled. Skipping.");
			return;
		}

		await ProcessBatchAsync(options: currentOptions, ct: executionContext.CancellationToken);
	}

	private async Task ProcessBatchAsync(DomainEventOutboxPublisherJobOptions options, CancellationToken ct)
	{
		IReadOnlyList<PendingDomainEvent> events = await readRepository.GetPendingBatchAsync(
			batchSize: options.BatchSize,
			ct: ct
		);

		if (events.Count == 0)
			return;

		logger.ZLogInformation(message: $"Publishing {events.Count} domain event(s).");

		int published = 0;

		foreach (PendingDomainEvent @event in events)
		{
			if (ct.IsCancellationRequested)
				break;

			Stopwatch sw = Stopwatch.StartNew();
			try
			{
				await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
				{
					await publisher.PublishAsync(message: new DomainEventMessage(
						MessageId: @event.Id,
						EventType: @event.EventType,
						AggregateId: @event.AggregateId,
						AggregateType: @event.AggregateType,
						CorrelationId: @event.CorrelationId,
						Payload: @event.Payload,
						OccurredAt: @event.OccurredAt
					), correlationId: @event.CorrelationId, ct: ct);

					await writeRepository.MarkAsProcessedAsync(
						id: @event.Id,
						processedAt: dateProvider.UtcNow,
						ct: ct
					);

					WorkerMetrics.OutboxPublished.Add(delta: 1);
					logger.ZLogInformation(message: $"Published: {++published}/{events.Count} ({@event.EventType}).");
				}, ct: ct);
			}
			catch (Exception exception)
			{
				if (ct.IsCancellationRequested)
					return;

				logger.ZLogError(exception: exception, message: $"Failed to publish domain event {@event.Id} ({@event.EventType}).");
				await UpdateRetryStateAsync(@event: @event, options: options, ct: ct);
			}
			finally
			{
				WorkerMetrics.MessageProcessingDuration.Record(value: sw.Elapsed.TotalMilliseconds);
			}
		}
	}

	private async Task UpdateRetryStateAsync(PendingDomainEvent @event, DomainEventOutboxPublisherJobOptions options, CancellationToken ct)
	{
		try
		{
			int newRetryCount = @event.RetryCount + 1;
			DateTimeOffset? failedAt = newRetryCount >= options.MaxRetries ? dateProvider.UtcNow : null;

			if (failedAt is not null)
			{
				logger.ZLogError(message: $"Domain event {@event.Id} exceeded max retries ({options.MaxRetries}). Moving to unresolvable events.");

				await unresolvableEventWriteRepository.CreateAsync(
					type: UnresolvableEventType.OutboxDeadLetter,
					referenceId: @event.Id,
					reason: $"Max retries ({options.MaxRetries}) exceeded.",
					payload: @event.Payload,
					occurredAt: dateProvider.UtcNow,
					ct: ct
				);
			}

			await writeRepository.MarkAsFailedAsync(
				id: @event.Id,
				retryCount: newRetryCount,
				failedAt: failedAt,
				ct: ct
			);
		}
		catch (Exception innerException)
		{
			logger.ZLogError(exception: innerException, message: $"Failed to update retry state for domain event {@event.Id}.");
		}
	}
}
