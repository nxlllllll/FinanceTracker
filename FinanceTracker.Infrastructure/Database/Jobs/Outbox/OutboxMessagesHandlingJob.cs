using System.Collections.Frozen;
using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.Jobs.Outbox;
 
[DisallowConcurrentExecution]
public sealed class OutboxMessagesHandlingJob(
	FinanceTrackerContext context,
	INotificationDispatcher dispatcher,
	IEventTypeResolver resolver,
	IUnitOfWork unitOfWork,
	IEnumerable<IAggregateNotificationFactory> factories,
	IDateProvider dateProvider,
	ILogger<OutboxMessagesHandlingJob> logger
) : IJob
{
	private const int Limit = 20;
	private const int MaxRetries = 5;

	private readonly FrozenDictionary<string, IAggregateNotificationFactory> _factories = factories.ToFrozenDictionary(keySelector: f => f.AggregateType);
	
	private IAppNotification BuildNotification(OutboxMessageEntity message)
	{
		OutboxPayload payload = JsonSerializer.Deserialize<OutboxPayload>(json: message.Payload)
			?? throw new SerializationException(message: "Failed to deserialize outbox payload.");
 
		List<IEvent> events = payload.Events.Select(selector: envelope =>
		{
			Type type = resolver.ResolveType(typeName: envelope.EventType);
			return (IEvent)JsonSerializer.Deserialize(json: envelope.EventPayload, returnType: type)!;
		}).ToList();
 
		if (!_factories.TryGetValue(key: message.AggregateType, value: out IAggregateNotificationFactory? factory))
			throw new UnknownAggregateTypeException(message: "No notification factory registered for aggregate type.", aggregateType: message.AggregateType);
		
		return factory.Build(aggregateId: message.AggregateId, events: events);
	}

	internal async Task ProcessMessagesAsync(CancellationToken ct)
	{
		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () => await ProcessBatchAsync(ct: ct),
			onError: async exception => logger.ZLogError(exception: exception, message: $"Outbox batch processing failed."),
			ct: ct
		);
	}
 
	private async Task ProcessBatchAsync(CancellationToken ct)
	{
		List<OutboxMessageEntity> messages = await context.WithSkipLocked<OutboxMessageEntity>()
			.Where(predicate: m => m.ProcessedAt == null && m.FailedAt == null)
			.OrderBy(keySelector: m => m.UpdatedAt)
			.Take(count: Limit)
			.ToListAsync(cancellationToken: ct);
 
		if (messages.Count == 0)
			return;
 
		logger.ZLogInformation(message: $"Found {messages.Count} outbox message(s) to process.");
 
		int processed = 0;
		foreach (OutboxMessageEntity message in messages)
		{
			await unitOfWork.ExecuteInTransactionAsync(
				operation: async () =>
				{
					await ProcessMessageAsync(message: message, ct: ct);
					logger.ZLogInformation(message: $"Outbox batch processed: {++processed}/{messages.Count}.");
				},
				onError: async exception => await UpdateRetryStateAsync(message: message, exception: exception, ct: ct),
				ct: ct
			);
		}
	}
 
	private async Task ProcessMessageAsync(OutboxMessageEntity message, CancellationToken ct)
	{
		IAppNotification appNotification = BuildNotification(message: message);
		await dispatcher.DispatchAsync(appNotification: appNotification, ct: ct);
		message.ProcessedAt = dateProvider.UtcNow;
		await context.SaveChangesAsync(cancellationToken: ct);
	}
 
	private async Task UpdateRetryStateAsync(OutboxMessageEntity message, Exception exception, CancellationToken ct)
	{
		if (ct.IsCancellationRequested)
			return;
 
		logger.ZLogError(exception: exception, message: $"Failed to process outbox message {message.Id}.");
 
		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () =>
			{
				++message.RetryCount;
				if (message.RetryCount >= MaxRetries)
				{
					message.FailedAt = dateProvider.UtcNow;
					logger.ZLogError(message: $"Outbox message {message.Id} moved to dead letter after {MaxRetries} retries.");
				}
 
				await context.SaveChangesAsync(cancellationToken: ct);
			},
			onError: async innerException => logger.ZLogError(exception: innerException, message: $"Failed to update retry state for outbox message {message.Id}."),
			ct: ct
		);
	}
	
	public async Task Execute(IJobExecutionContext context)
		=> await ProcessMessagesAsync(ct: context.CancellationToken);
}