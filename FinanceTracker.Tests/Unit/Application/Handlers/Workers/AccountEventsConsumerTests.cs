using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
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
	private IEventTypeResolver _eventTypeResolver = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_accountWriteRepository = Substitute.For<IAccountWriteRepository>();
		_eventTypeResolver = Substitute.For<IEventTypeResolver>();

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
			eventTypeResolver: _eventTypeResolver,
			context: Context,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<AccountEventsConsumer>>()
		);
	}

	private static AccountEventsMessage BuildMessage(Guid? messageId = null)
	{
		return new AccountEventsMessage(
			MessageId: messageId ?? Guid.CreateVersion7(),
			AggregateId: Guid.CreateVersion7(),
			Events: []
		);
	}

	[Test]
	public async Task HandleAsync_WhenMessageAlreadyProcessed_ShouldNotCallProjection()
	{
		Guid messageId = Guid.CreateVersion7();

		await Context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
		{
			MessageId = messageId,
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
			ProcessedAt = FakeDateProvider.Default.UtcNow
		});
		await Context.SaveChangesAsync();

		int countBefore = Context.ProcessedMessages.Count();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		int countAfter = Context.ProcessedMessages.Count();

		await Assert.That(value: countAfter).IsEqualTo(expected: countBefore);
	}

	[Test]
	public async Task HandleAsync_WhenMessageNotProcessed_ShouldCallExecuteInTransaction()
	{
		await _consumer.HandleAsync(
			message: BuildMessage(),
			ct: CancellationToken.None
		);

		await _unitOfWork.Received(requiredNumberOfCalls: 1).ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		);
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
		Guid messageId = Guid.CreateVersion7();
		AccountEventsMessage message = BuildMessage(messageId: messageId);

		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);
		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

		int count = Context.ProcessedMessages.Count(predicate: m => m.MessageId == messageId);

		await Assert.That(value: count).IsEqualTo(expected: 1);
	}
	
	[Test]
	public async Task AccountEventsConsumer_ShouldImplement_IMessageHandler()
	{
		await Assert.That(value: _consumer is IMessageHandler<AccountEventsMessage> result).IsTrue();
	}
}