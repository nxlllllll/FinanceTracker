using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account.Notification;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Database.Workers.Outbox;
 
public sealed class OutboxWorker(
	IServiceScopeFactory scopeFactory,
	ILogger<OutboxWorker> logger
) : BackgroundService
{
	private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(value: 3);
 
	private static AccountNotification BuildNotification(
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
 
		return new AccountNotification(
			AccountId: message.AggregateId,
			Events: events
		);
	}
 
	internal async Task ProcessBatchAsync(CancellationToken ct)
	{
		using IServiceScope scope = scopeFactory.CreateScope();
 
		FinanceTrackerContext context = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();
		INotificationDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
		IEventTypeResolver resolver = scope.ServiceProvider.GetRequiredService<IEventTypeResolver>();
		IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
 
		await unitOfWork.BeginTransactionAsync(ct: ct);
 
		List<OutboxMessageEntity> messages = await context.OutboxMessages.FromSqlRaw(sql: """
			SELECT * FROM outbox_messages
			WHERE processed_at IS NULL
			ORDER BY created_at
			LIMIT 20
			FOR UPDATE SKIP LOCKED
		""").ToListAsync(cancellationToken: ct);
 
		if (messages.Count == 0)	
		{
			await unitOfWork.RollbackAsync(ct: ct);
			return;
		}
 
		foreach (OutboxMessageEntity message in messages)
		{
			try
			{
				AccountNotification notification = BuildNotification(
					message: message,
					resolver: resolver
				);
 
				await dispatcher.DispatchAsync(notification: new Notification(Data: notification), ct: ct);
				message.ProcessedAt = DateTime.UtcNow;
			}
			catch (Exception exception)
			{
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