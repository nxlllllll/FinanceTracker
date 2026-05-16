using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.AccountProjection.Consumers;
using FinanceTracker.Worker.AccountProjection.Projection;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Workers;

public sealed class AccountEventsConsumerTests : DatabaseFixture
{
	private AccountEventsConsumer _consumer = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IAccountWriteRepository _accountWriteRepository = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_accountWriteRepository = Substitute.For<IAccountWriteRepository>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: call => call.Arg<Func<Task>>()());

		AccountProjection projection = new AccountProjection(
			accountWriteRepository: _accountWriteRepository,
			logger: Substitute.For<ILogger<AccountProjection>>()
		);

		_consumer = new AccountEventsConsumer(
			projection: projection,
			eventTypeResolver: Substitute.For<IEventTypeResolver>(),
			context: Context,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<AccountEventsConsumer>>()
		);
	}
	
	private static AggregateEventsMessage BuildMessage(
		Guid? messageId = null,
		string aggregateType = AggregateTypeNames.Account)
	{
		return new AggregateEventsMessage(
			MessageId: messageId ?? Guid.CreateVersion7(),
			AggregateId: Guid.CreateVersion7(),
			AggregateType: aggregateType,
			CorrelationId: Guid.CreateVersion7(),
			Events: []
		);
	}

	[Test]
	public async Task AccountEventsConsumer_ShouldImplement_IMessageHandler()
		=> await Assert.That(value: _consumer is IMessageHandler<AggregateEventsMessage> result).IsTrue();
	
	[Test]
	public async Task HandleAsync_WhenAggregateTypeIsNotAccount_ShouldSkipWithoutTransaction()
	{
		AggregateEventsMessage message = BuildMessage(aggregateType: AggregateTypeNames.Transaction);

		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

		await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAggregateTypeIsNotAccount_ShouldNotCallProjection()
	{
		await _consumer.HandleAsync(
			message: BuildMessage(aggregateType: AggregateTypeNames.Budget),
			ct: CancellationToken.None
		);

		await _accountWriteRepository.DidNotReceive().CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	[Arguments(nameof(AggregateTypeNames.Transaction))]
	[Arguments(nameof(AggregateTypeNames.Budget))]
	[Arguments(nameof(AggregateTypeNames.Category))]
	[Arguments(nameof(AggregateTypeNames.User))]
	[Arguments("UnknownAggregate")]
	public async Task HandleAsync_WhenAggregateTypeIsNotAccount_ShouldAlwaysSkip(string aggregateType)
	{
		await _consumer.HandleAsync(
			message: BuildMessage(aggregateType: aggregateType),
			ct: CancellationToken.None
		);

		await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAggregateTypeIsAccount_ShouldExecuteTransaction()
	{
		await _consumer.HandleAsync(
			message: BuildMessage(aggregateType: AggregateTypeNames.Account),
			ct: CancellationToken.None
		);

		await _unitOfWork.Received(requiredNumberOfCalls: 1).ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		);
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

		int countBefore = Context.ProcessedMessages.Count();

		await _consumer.HandleAsync(
			message: BuildMessage(messageId: messageId),
			ct: CancellationToken.None
		);

		await Assert.That(value: Context.ProcessedMessages.Count()).IsEqualTo(expected: countBefore);
	}

	[Test]
	public async Task HandleAsync_WhenMessageNotProcessed_ShouldSaveProcessedMessage()
	{
		Guid messageId = Guid.CreateVersion7();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		bool saved = Context.ProcessedMessages.Any(predicate: m => m.MessageId == messageId);

		await Assert.That(value: saved).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenCalledTwiceWithSameId_ShouldSaveProcessedMessageOnce()
	{
		AggregateEventsMessage message = BuildMessage();

		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);
		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

		int count = Context.ProcessedMessages.Count(predicate: m => m.MessageId == message.MessageId);

		await Assert.That(value: count).IsEqualTo(expected: 1);
	}
}