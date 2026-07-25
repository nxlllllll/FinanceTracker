using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context.ProcessedMessage;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Repositories.ProcessedMessage;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.PermissionProjection.Consumer;
using FinanceTracker.Worker.PermissionProjection.Projection;
using FinanceTracker.Worker.Shared.Projection;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class PermissionEventsConsumerTests : DatabaseFixture
{
	private PermissionEventsConsumer _consumer = null!;
	private IUserPermissionWriteRepository _writeRepository = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IUserPermissionWriteRepository>();

		IConnectionMultiplexer connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		IDatabase database = Substitute.For<IDatabase>();
		connectionMultiplexer.GetDatabase(
			db: Arg.Any<int>(),
			asyncState: Arg.Any<object>()
		).Returns(returnThis: database);
		database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: RedisValue.Null);
		database.KeyDeleteAsync(keys: Arg.Any<RedisKey[]>()).Returns(returnThis: 1L);

		IOptionsMonitor<RedisOptions> redisOptions = Substitute.For<IOptionsMonitor<RedisOptions>>();
		redisOptions.CurrentValue.Returns(returnThis: new RedisOptions
		{
			InstanceName = "ft_test:"
		});

		RedisCache redisCache = new RedisCache(
			connectionMultiplexer: connectionMultiplexer,
			options: redisOptions,
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<RedisCache>.Instance
		);

		PermissionEventApplier applier = new PermissionEventApplier(repository: _writeRepository, redisCache: redisCache);

		PermissionProjection projection = new PermissionProjection(
			applier: applier,
			logger: Substitute.For<ILogger<PermissionProjection>>()
		);

		_consumer = new PermissionEventsConsumer(
			projection: projection,
			integrationEventTypeResolver: new IntegrationEventTypeResolver(
				contractsAssembly: typeof(IIntegrationEvent).Assembly,
				logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
			),
			processedMessageReadRepository: new ProcessedMessageReadRepository(context: Context),
			processedMessageWriteRepository: new ProcessedMessageWriteRepository(context: Context),
			unitOfWork: UnitOfWork,
			dateProvider: FakeDateProvider.Default,
			retryOptions: new FakeOptionsMonitor<ProjectionRetryOptions>(value: new ProjectionRetryOptions
			{
				MaxRetries = 3,
				BaseDelayMs = 10,
				UseJitter = false
			}),
			logger: Substitute.For<ILogger<PermissionEventsConsumer>>()
		);
	}

	private static AggregateEventsMessage BuildMessage(Guid? messageId = null, Guid? aggregateId = null)
	{
		return new AggregateEventsMessage(
			MessageId: messageId ?? Guid.CreateVersion7(),
			AggregateId: aggregateId ?? Guid.CreateVersion7(),
			AggregateType: AggregateTypeNames.UserPermission,
			CorrelationId: Guid.CreateVersion7(),
			Events: []
		);
	}

	[Test]
	public async Task PermissionEventsConsumer_ShouldImplement_IMessageHandler()
		=> await Assert.That(value: _consumer is IMessageHandler<AggregateEventsMessage> result).IsTrue();

	[Test]
	public async Task HandleAsync_WhenMessageNotProcessed_ShouldSaveProcessedMessage()
	{
		Guid messageId = Guid.CreateVersion7();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		bool saved = await Context.ProcessedMessages.AnyAsync(
			predicate: m => m.MessageId == messageId && m.ConsumerType == nameof(PermissionEventsConsumer)
		);

		await Assert.That(value: saved).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenMessageAlreadyProcessed_ShouldNotCallProjection()
	{
		Guid messageId = Guid.CreateVersion7();

		await Context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
		{
			MessageId = messageId,
			ConsumerType = nameof(PermissionEventsConsumer),
			ProcessedAt = FakeDateProvider.Default.UtcNow
		});
		await Context.SaveChangesAsync();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		await _writeRepository.DidNotReceive().GrantAsync(
			@event: Arg.Any<PermissionGranted>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenMessageAlreadyProcessed_ShouldNotAddSecondProcessedMessage()
	{
		Guid messageId = Guid.CreateVersion7();

		await Context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
		{
			MessageId = messageId,
			ConsumerType = nameof(PermissionEventsConsumer),
			ProcessedAt = FakeDateProvider.Default.UtcNow
		});
		await Context.SaveChangesAsync();

		int countBefore = await Context.ProcessedMessages.CountAsync();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		int countAfter = await Context.ProcessedMessages.CountAsync();

		await Assert.That(value: countAfter).IsEqualTo(expected: countBefore);
	}

	[Test]
	public async Task HandleAsync_WhenCalledTwiceWithSameId_ShouldSaveProcessedMessageOnce()
	{
		AggregateEventsMessage message = BuildMessage();

		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);
		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

		int count = await Context.ProcessedMessages.CountAsync(
			predicate: m => m.MessageId == message.MessageId && m.ConsumerType == nameof(PermissionEventsConsumer)
		);

		await Assert.That(value: count).IsEqualTo(expected: 1);
	}
}
