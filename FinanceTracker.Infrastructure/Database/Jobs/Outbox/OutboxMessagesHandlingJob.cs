using System.Collections.Frozen;
using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
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
		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			List<OutboxMessageEntity> messages = await databaseContext.WithSkipLocked<OutboxMessageEntity>()
				.Where(predicate: m => m.ProcessedAt == null && m.FailedAt == null)
				.OrderBy(keySelector: m => m.UpdatedAt)
				.Take(count: Limit)
				.ToListAsync(cancellationToken: ct);
 
			foreach (OutboxMessageEntity message in messages)
			{
				await unitOfWork.ExecuteInTransactionAsync(operation: async () => 
				{
					IAppNotification appNotification = BuildNotification(message: message); 
					await dispatcher.DispatchAsync(appNotification: appNotification, ct: ct); 
					message.ProcessedAt = dateProvider.UtcNow; 
					await databaseContext.SaveChangesAsync(cancellationToken: ct);
				}, onError: async _ =>
				{
					if (ct.IsCancellationRequested)
						return;
				
					await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
					{
						++message.RetryCount;
						if (message.RetryCount >= MaxRetries)
							message.FailedAt = dateProvider.UtcNow;
				
						await databaseContext.SaveChangesAsync(cancellationToken: ct);
					}, ct: ct);
				}, ct: ct);
			}
		}, ct: ct);
	}
	
	public async Task Execute(IJobExecutionContext context)
		=> await ProcessMessagesAsync(ct: context.CancellationToken);
}