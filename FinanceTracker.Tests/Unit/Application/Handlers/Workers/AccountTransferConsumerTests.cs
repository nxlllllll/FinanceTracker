using System.Text.Json;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.AccountProjection.Consumers;
using FinanceTracker.Worker.TransferProjection.Consumers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Workers;

public sealed class AccountTransferConsumerTests : DatabaseFixture
{
	private IAccountRepository _accountRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private AccountTransferConsumer _consumer = null!;

	private static readonly Guid TransferId = Guid.CreateVersion7();
	private static readonly Guid FromAccountId = Guid.CreateVersion7();
	private static readonly Guid ToAccountId = Guid.CreateVersion7();

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: call => call.Arg<Func<Task>>()());

		_consumer = new AccountTransferConsumer(
			accountRepository: _accountRepository,
			eventTypeResolver: new EventTypeResolver(
				assembly: typeof(FinanceTracker.Core.Domains.Account.Account).Assembly,
				logger: Substitute.For<ILogger<EventTypeResolver>>()
			),
			unitOfWork: _unitOfWork,
			context: Context,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<AccountTransferConsumer>>()
		);
	}

	private AggregateEventsMessage BuildMessage(
		Guid? messageId = null,
		string aggregateType = AggregateTypeNames.Account,
		bool includeDebitEvent = true)
	{
		List<EventEnvelope> events = [];

		if (includeDebitEvent)
		{
			AccountTransferDebited debitEvent = new AccountTransferDebited(
				Id: Guid.CreateVersion7(),
				AccountId: FromAccountId,
				TransferId: TransferId,
				ToAccountId: ToAccountId,
				Amount: 1000m,
				ForexRate: 1m,
				Description: "Test",
				OccurredAt: FakeDateProvider.Default.UtcNow
			);

			events.Add(item: new EventEnvelope(
				EventType: "account.transfer_debited",
				EventPayload: JsonSerializer.Serialize(
					value: debitEvent,
					options: FinanceTrackerJsonOptions.Payload
				)
			));
		}

		return new AggregateEventsMessage(
			MessageId: messageId ?? Guid.CreateVersion7(),
			AggregateId: FromAccountId,
			AggregateType: aggregateType,
			CorrelationId: Guid.CreateVersion7(),
			Events: events
		);
	}
	
	[Test]
	public async Task HandleAsync_WhenNotAccountAggregate_ShouldSkip()
	{
		await _consumer.HandleAsync(message: BuildMessage(aggregateType: "transaction"), ct: CancellationToken.None);

		await _accountRepository.DidNotReceive().GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenNoDebitEvent_ShouldSkip()
	{
		await _consumer.HandleAsync(message: BuildMessage(includeDebitEvent: false), ct: CancellationToken.None);

		await _accountRepository.DidNotReceive().GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAlreadyProcessedByThisConsumer_ShouldSkip()
	{
		Guid messageId = Guid.CreateVersion7();
		await Context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
		{
			MessageId = messageId,
			ConsumerType = nameof(AccountTransferConsumer),
			ProcessedAt = FakeDateProvider.Default.UtcNow
		});
		await Context.SaveChangesAsync();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		await _accountRepository.DidNotReceive().GetByIdAsync(
			accountId: ToAccountId,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenProcessedByDifferentConsumer_ShouldNotSkip()
	{
		Guid messageId = Guid.CreateVersion7();
		await Context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
		{
			MessageId = messageId,
			ConsumerType = nameof(AccountEventsConsumer),
			ProcessedAt = FakeDateProvider.Default.UtcNow
		});
		await Context.SaveChangesAsync();

		_accountRepository.GetByIdAsync(
			accountId: ToAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		await _accountRepository.Received(requiredNumberOfCalls: 1).GetByIdAsync(
			accountId: ToAccountId,
			ct: Arg.Any<CancellationToken>()
		);
	}
	
	[Test]
	public async Task HandleAsync_WhenCreditSucceeds_ShouldSaveToAccount()
	{
		FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.Create().Value!;

		_accountRepository.GetByIdAsync(
			accountId: ToAccountId, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: toAccount);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: toAccount,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCreditSucceeds_ShouldSaveProcessedMessage()
	{
		Guid messageId = Guid.CreateVersion7();
		_accountRepository.GetByIdAsync(
			accountId: ToAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		bool saved = Context.ProcessedMessages.Any(
			predicate: m => m.MessageId == messageId && m.ConsumerType == nameof(AccountTransferConsumer)
		);
		await Assert.That(value: saved).IsTrue();
	}
	
	[Test]
	public async Task HandleAsync_WhenToAccountNotFound_ShouldCompensate()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.Create().Value!;
		decimal balanceBefore = fromAccount.Balance.Amount;

		_accountRepository.GetByIdAsync(
			accountId: ToAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.Account.Account?)null);
		_accountRepository.GetByIdAsync(
			accountId: FromAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: fromAccount);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await Assert.That(value: fromAccount.Balance.Amount).IsGreaterThan(minimum: balanceBefore);
	}
}