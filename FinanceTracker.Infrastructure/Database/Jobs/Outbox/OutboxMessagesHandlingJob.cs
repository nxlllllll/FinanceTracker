using System.Collections.Frozen;
using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace FinanceTracker.Infrastructure.Database.Jobs.Outbox;
 
[DisallowConcurrentExecution]
public sealed class OutboxMessagesHandlingJob(
	FinanceTrackerContext databaseContext,
	INotificationDispatcher dispatcher,
	IEventTypeResolver resolver,
	IUnitOfWork unitOfWork,
	IEnumerable<IAggregateNotificationFactory> factories,
	IDateProvider dateProvider
) : IJob
{
	private const int Limit = 20;
	private const int MaxRetries = 5;

	private readonly FrozenDictionary<string, IAggregateNotificationFactory> _factories =
		factories.ToFrozenDictionary(keySelector: f => f.AggregateType);
	
	private Notification BuildNotification(OutboxMessageEntity message)
	{
		OutboxPayload payload = JsonSerializer.Deserialize<OutboxPayload>(json: message.Payload)
			?? throw new SerializationException(message: "Failed to deserialize outbox payload.");
 
		List<IEvent> events = payload.Events.Select(selector: envelope =>
		{
			Type type = resolver.ResolveType(typeName: envelope.EventType);
			return (IEvent)JsonSerializer.Deserialize(json: envelope.EventPayload, returnType: type)!;
		}).ToList();
 
		if (!_factories.TryGetValue(key: message.AggregateType, value: out IAggregateNotificationFactory? factory))
			throw new InvalidOperationException(message: $"No notification factory registered for aggregate type: '{message.AggregateType}'.");
		
		return new Notification(Data: factory.Build(aggregateId: message.AggregateId, events: events));
	}

	internal async Task ProcessMessagesAsync(CancellationToken ct)
	{
		await unitOfWork.BeginTransactionAsync(ct: ct);
 
		List<OutboxMessageEntity> messages = await databaseContext.WithSkipLocked<OutboxMessageEntity>()
			.Where(predicate: m => m.ProcessedAt == null && m.FailedAt == null)
			.OrderBy(keySelector: m => m.UpdatedAt)
			.Take(count: Limit)
			.ToListAsync(cancellationToken: ct);
 
		if (messages.Count == 0)	
		{
			await unitOfWork.RollbackAsync(ct: ct);
			return;
		}
		
		foreach (OutboxMessageEntity message in messages)
		{
			try
			{
				Notification notification = BuildNotification(message: message);
 
				await dispatcher.DispatchAsync(notification: notification, ct: ct);
				message.ProcessedAt = dateProvider.UtcNow;
			}
			catch
			{
				++message.RetryCount;
				
				if (message.RetryCount >= MaxRetries)
					message.FailedAt = dateProvider.UtcNow;
			}
		}

		try
		{
			await databaseContext.SaveChangesAsync(cancellationToken: ct);
			await unitOfWork.CommitAsync(ct: ct);
		}
		catch
		{
			await unitOfWork.RollbackAsync(ct: ct);
		}
	}
	
	public async Task Execute(IJobExecutionContext context)
		=> await ProcessMessagesAsync(ct: context.CancellationToken);
}