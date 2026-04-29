using System.Collections.Frozen;
using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Database.Workers.Outbox;
 
public sealed class OutboxWorker(
	IServiceScopeFactory scopeFactory,
	IEnumerable<IAggregateNotificationFactory> factories,
	ILogger<OutboxWorker> logger
) : BackgroundService
{
	private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(value: 3);
	private const int Limit = 20;
	private const int MaxRetries = 5;

	private readonly FrozenDictionary<string, IAggregateNotificationFactory> _factories =
		factories.ToFrozenDictionary(keySelector: f => f.AggregateType);
	
	private Notification BuildNotification(
		OutboxMessageEntity message,
		IEventTypeResolver resolver)
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
 
	internal async Task ProcessBatchAsync(CancellationToken ct)
	{
		using IServiceScope scope = scopeFactory.CreateScope();
 
		FinanceTrackerContext context = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();
		INotificationDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
		IEventTypeResolver resolver = scope.ServiceProvider.GetRequiredService<IEventTypeResolver>();
		IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
 
		await unitOfWork.BeginTransactionAsync(ct: ct);
 
		List<OutboxMessageEntity> messages = await context.WithSkipLocked<OutboxMessageEntity>()
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
				Notification notification = BuildNotification(
					message: message,
					resolver: resolver
				);
 
				await dispatcher.DispatchAsync(notification: notification, ct: ct);
				message.ProcessedAt = DateTime.UtcNow;
			}
			catch (Exception exception)
			{
				++message.RetryCount;
				
				if (message.RetryCount >= MaxRetries)
					message.FailedAt = DateTime.UtcNow;
				logger.LogError(exception: exception, message: "Failed to process outbox message: {messageId}.", message.Id);
			}
		}

		try
		{
			await context.SaveChangesAsync(cancellationToken: ct);
			await unitOfWork.CommitAsync(ct: ct);
		}
		catch (Exception exception)
		{
			await unitOfWork.RollbackAsync(ct: ct);
			logger.LogError(exception: exception, message: "Failed to commit outbox batch.");
		}
	}
 
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessBatchAsync(ct: stoppingToken);
			}
			catch (Exception exception)
			{
				logger.LogError(exception: exception, message: "OutboxWorker error: {Message}.", exception.Message);
			}
 
			await Task.Delay(delay: PollingInterval, cancellationToken: stoppingToken);
		}
	}
}