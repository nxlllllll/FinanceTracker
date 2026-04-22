using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Database.Outbox;

public sealed class OutboxWorker(
	IServiceScopeFactory scopeFactory,
	ILogger<OutboxWorker> logger
) : BackgroundService
{
	private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(value: 3);

	private static AggregateNotification BuildNotification(
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

		return new AggregateNotification(
			AggregateId: message.AggregateId,
			AggregateType: message.AggregateType,
			Events: events
		);
	}
	
	internal async Task ProcessBatchAsync(CancellationToken ct)
	{
		using IServiceScope scope = scopeFactory.CreateScope();

		FinanceTrackerContext context = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();
		INotificationDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
		IEventTypeResolver resolver = scope.ServiceProvider.GetRequiredService<IEventTypeResolver>();
		
		List<OutboxMessageEntity> messages = await context.OutboxMessages.FromSqlRaw(sql: """
			SELECT * FROM outbox_messages
			WHERE processed_at IS NULL
			ORDER BY created_at
			LIMIT 20
			FOR UPDATE SKIP LOCKED
		""").ToListAsync(cancellationToken: ct);
		
		if (messages.Count == 0)
			return;

		foreach (OutboxMessageEntity message in messages)
		{
			await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken: ct);
			try
			{
				AggregateNotification notification = BuildNotification(
					message: message,
					resolver: resolver
				);

				await dispatcher.DispatchAsync(notification: notification, ct: ct);
				message.ProcessedAt = DateTime.UtcNow;
				await context.SaveChangesAsync(cancellationToken: ct);
				await transaction.CommitAsync(cancellationToken: ct);
			}
			catch (Exception exception)
			{
				await transaction.RollbackAsync(cancellationToken: ct);
				logger.LogError(exception: exception, message: "Failed to process outbox message: {messageId}.", message.Id);
			}
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
				logger.LogError(exception: exception, message: "OutboxWorker error: {message}.", exception.Message);
			}
			
			await Task.Delay(delay: PollingInterval, cancellationToken: stoppingToken);
		}
	}
}