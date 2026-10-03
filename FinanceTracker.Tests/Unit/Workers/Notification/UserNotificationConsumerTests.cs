using System.Text.Json;
using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Events.Budget;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Services.Notification;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Worker.Notification.Consumer;
using FinanceTracker.Worker.Notification.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Workers.Notification;

public sealed class UserNotificationConsumerTests
{
	private static readonly IntegrationEventTypeResolver Resolver = new IntegrationEventTypeResolver(
		contractsAssembly: typeof(IIntegrationEvent).Assembly,
		logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
	);

	private IUserNotificationMapper _budgetMapper = null!;
	private INotificationDispatcher _dispatcher = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetMapper = Substitute.For<IUserNotificationMapper>();
		_budgetMapper.NotificationType.Returns(returnThis: typeof(BudgetThresholdReachedEvent));

		_dispatcher = Substitute.For<INotificationDispatcher>();
	}

	private UserNotificationConsumer CreateConsumer(params IUserNotificationMapper[] mappers) => new UserNotificationConsumer(
		mappers: mappers,
		notificationDispatcher: _dispatcher,
		integrationEventTypeResolver: Resolver,
		logger: NullLogger<UserNotificationConsumer>.Instance
	);

	private static EventEnvelope Envelope(IIntegrationEvent integrationEvent) => new EventEnvelope(
		EventType: Resolver.ResolveTypeName(eventType: integrationEvent.GetType()),
		EventPayload: JsonSerializer.Serialize(value: integrationEvent, inputType: integrationEvent.GetType(), options: FinanceTrackerJsonOptions.Payload)
	);

	private static AggregateEventsMessage Message(params EventEnvelope[] envelopes) => new AggregateEventsMessage(
		MessageId: Guid.CreateVersion7(),
		AggregateId: Guid.CreateVersion7(),
		AggregateType: IUserNotification.RoutingKey,
		CorrelationId: Guid.CreateVersion7(),
		Events: [..envelopes]
	);

	[Test]
	public async Task HandleAsync_ShouldDispatchWhatTheMapperProduces()
	{
		BudgetThresholdReachedEvent alert = BudgetThresholdReachedMapperTests.Alert(threshold: 80, spent: 8_500m);
		NotificationRequest request = new NotificationRequest(
			EventId: alert.EventId,
			UserId: alert.UserId,
			Template: "BudgetThresholdReached",
			Values: new Dictionary<string, string>()
		);

		_budgetMapper.MapAsync(
			notification: Arg.Is<IUserNotification>(n => n!.EventId == alert.EventId),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: request);

		await CreateConsumer(_budgetMapper).HandleAsync(message: Message(Envelope(integrationEvent: alert)));

		await _dispatcher.Received(requiredNumberOfCalls: 1).SendAsync(request: request, ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WithAnUnrecognisedEventType_ShouldSkipIt()
	{
		await CreateConsumer(_budgetMapper).HandleAsync(message: Message(new EventEnvelope(EventType: "something.unheard_of", EventPayload: "{}")));

		await _dispatcher.DidNotReceiveWithAnyArgs().SendAsync(request: null!);
	}

	[Test]
	public async Task HandleAsync_WithANotificationNoMapperHandles_ShouldThrow()
	{
		BudgetThresholdReachedEvent alert = BudgetThresholdReachedMapperTests.Alert(threshold: 80, spent: 8_500m);

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () =>
			await CreateConsumer().HandleAsync(message: Message(Envelope(integrationEvent: alert)))
		).Because(message: """
			A notification without a mapper is a deployment defect. Skipping it would ack the message and the
			user would never hear about it; throwing leaves it in the dead-letter queue, where it is visible.
		""");
	}
}
