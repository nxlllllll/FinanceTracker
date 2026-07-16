using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Context.ProcessedMessage;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Repositories.ProcessedMessage;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.AccountProjection.Consumer;
using FinanceTracker.Worker.AccountProjection.Projection;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class AccountEventsConsumerTests : DatabaseFixture
{
	private AccountEventsConsumer _consumer = null!;
	private IAccountWriteRepository _accountWriteRepository = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountWriteRepository = Substitute.For<IAccountWriteRepository>();

		AccountEventApplier applier = new AccountEventApplier(repository: _accountWriteRepository);

		AccountProjection projection = new AccountProjection(
			applier: applier,
			logger: Substitute.For<ILogger<AccountProjection>>()
		);

		_consumer = new AccountEventsConsumer(
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
			logger: Substitute.For<ILogger<AccountEventsConsumer>>()
		);
	}

	private static AggregateEventsMessage BuildMessage(Guid? messageId = null)
	{
		return new AggregateEventsMessage(
			MessageId: messageId ?? Guid.CreateVersion7(),
			AggregateId: Guid.CreateVersion7(),
			AggregateType: AggregateTypeNames.Account,
			CorrelationId: Guid.CreateVersion7(),
			Events: []
		);
	}

	[Test]
	public async Task AccountEventsConsumer_ShouldImplement_IMessageHandler()
		=> await Assert.That(value: _consumer is IMessageHandler<AggregateEventsMessage> result).IsTrue();

	[Test]
	public async Task HandleAsync_WhenAggregateTypeIsAccount_ShouldExecuteTransaction()
	{
		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		bool saved = await Context.ProcessedMessages.AnyAsync();
		await Assert.That(value: saved).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenMessageAlreadyProcessed_ShouldNotCallProjection()
	{
		Guid messageId = Guid.CreateVersion7();

		await Context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
		{
			MessageId = messageId,
			ConsumerType = nameof(AccountEventsConsumer),
			ProcessedAt = FakeDateProvider.Default.UtcNow
		});
		await Context.SaveChangesAsync();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		await _accountWriteRepository.DidNotReceive().CreateAsync(
			@event: Arg.Any<AccountCreated>(),
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
			ConsumerType = nameof(AccountEventsConsumer),
			ProcessedAt = FakeDateProvider.Default.UtcNow
		});
		await Context.SaveChangesAsync();

		int countBefore = await Context.ProcessedMessages.CountAsync();

		await _consumer.HandleAsync(
			message: BuildMessage(messageId: messageId),
			ct: CancellationToken.None
		);

		int countAfter = await Context.ProcessedMessages.CountAsync();

		await Assert.That(value: countAfter).IsEqualTo(expected: countBefore);
	}

	[Test]
	public async Task HandleAsync_WhenMessageNotProcessed_ShouldSaveProcessedMessage()
	{
		Guid messageId = Guid.CreateVersion7();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		bool saved = await Context.ProcessedMessages.AnyAsync(
			predicate: m => m.MessageId == messageId && m.ConsumerType == nameof(AccountEventsConsumer)
		);

		await Assert.That(value: saved).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenCalledTwiceWithSameId_ShouldSaveProcessedMessageOnce()
	{
		AggregateEventsMessage message = BuildMessage();

		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);
		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

		int count = await Context.ProcessedMessages.CountAsync(
			predicate: m => m.MessageId == message.MessageId && m.ConsumerType == nameof(AccountEventsConsumer)
		);

		await Assert.That(value: count).IsEqualTo(expected: 1);
	}
}
