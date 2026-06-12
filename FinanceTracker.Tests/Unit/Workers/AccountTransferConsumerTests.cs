using System.Text.Json;
using FinanceTracker.Contracts.Events.Account;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Services.TransferCompensation;
using FinanceTracker.Infrastructure.Database.Context.ProcessedMessage;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Repositories.ProcessedMessage;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.AccountProjection.Consumer;
using FinanceTracker.Worker.TransferProjection.Consumer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class AccountTransferConsumerTests : DatabaseFixture
{
	private IAccountRepository _accountRepository = null!;
	private ITransferRepository _transferRepository = null!;
	private ITransferWriteRepository _transferWriteRepository = null!;
	private ITransferCompensationService _compensationService = null!;
	private AccountTransferConsumer _consumer = null!;

	private static readonly Guid TransferId = Guid.CreateVersion7();
	private static readonly Guid FromAccountId = Guid.CreateVersion7();
	private static readonly Guid ToAccountId = Guid.CreateVersion7();

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_transferRepository = Substitute.For<ITransferRepository>();
		_transferWriteRepository = Substitute.For<ITransferWriteRepository>();
		_compensationService = Substitute.For<ITransferCompensationService>();

		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TransferFactory.Reconstitute(
			id: TransferId,
			fromAccountId: FromAccountId,
			toAccountId: ToAccountId
		));

		_consumer = new AccountTransferConsumer(
			accountRepository: _accountRepository,
			transferRepository: _transferRepository,
			transferWriteRepository: _transferWriteRepository,
			integrationEventTypeResolver: new IntegrationEventTypeResolver(
				contractsAssembly: typeof(IAccountIntegrationEvent).Assembly,
				logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
			),
			processedMessageReadRepository: new ProcessedMessageReadRepository(context: Context),
			processedMessageWriteRepository: new ProcessedMessageWriteRepository(context: Context),
			compensationService: _compensationService,
			unitOfWork: UnitOfWork,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<AccountTransferConsumer>>()
		);
	}

	private AggregateEventsMessage BuildMessage(
		Guid? messageId = null,
		bool includeDebitEvent = true)
	{
		List<EventEnvelope> events = [];

		if (includeDebitEvent)
		{
			AccountTransferDebitedEvent debitEvent = new AccountTransferDebitedEvent(
				EventId: Guid.CreateVersion7(),
				AccountId: FromAccountId,
				TransferId: TransferId,
				ToAccountId: ToAccountId,
				Amount: 1000m,
				ForexRate: 1m,
				Description: "Test",
				Version: 1,
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
			AggregateType: AggregateTypeNames.Account,
			CorrelationId: Guid.CreateVersion7(),
			Events: events
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

		await _transferRepository.Received(requiredNumberOfCalls: 1).GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenTransferNotFound_ShouldSkipWithoutTouchingAccounts()
	{
		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Transfer?)null);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _accountRepository.DidNotReceive().GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCreditSucceeds_ShouldSaveToAccount()
	{
		Account toAccount = AccountFactory.Create().Value!;

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
	public async Task HandleAsync_WhenCreditSucceeds_ShouldUpdateStatusToCompleted()
	{
		_accountRepository.GetByIdAsync(
			accountId: ToAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _transferWriteRepository.Received(requiredNumberOfCalls: 1).UpdateStatusAsync(
			transferId: TransferId,
			status: TransferStatus.Completed,
			expectedVersion: Arg.Any<int>(),
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

		bool saved = await Context.ProcessedMessages.AnyAsync(
			predicate: m => m.MessageId == messageId && m.ConsumerType == nameof(AccountTransferConsumer)
		);

		await Assert.That(value: saved).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenCreditSucceeds_ShouldNotSaveFromAccount()
	{
		Account toAccount = AccountFactory.Create().Value!;
		Account fromAccount = AccountFactory.Create().Value!;

		_accountRepository.GetByIdAsync(
			accountId: ToAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: toAccount);

		_accountRepository.GetByIdAsync(
			accountId: FromAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: fromAccount);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: fromAccount,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCreditSucceeds_ShouldNotCallCompensationService()
	{
		_accountRepository.GetByIdAsync(
			accountId: ToAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _compensationService.DidNotReceive().CompensateAsync(
			transfer: Arg.Any<PendingCreditTransfer>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenToAccountNotFound_ShouldDelegateToCompensationService()
	{
		_accountRepository.GetByIdAsync(
			accountId: ToAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Account?)null);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _compensationService.Received(requiredNumberOfCalls: 1).CompensateAsync(
			transfer: Arg.Is<PendingCreditTransfer>(t =>
				t.TransferId == TransferId &&
				t.FromAccountId == FromAccountId &&
				t.Amount == 1000m),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenToAccountNotFound_ShouldNotSaveAnyAccount()
	{
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Account?)null);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenTransferAlreadyCompleted_ShouldSkipWithoutSavingAccounts()
	{
		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TransferFactory.Reconstitute(
			id: TransferId,
			fromAccountId: FromAccountId,
			toAccountId: ToAccountId,
			status: TransferStatus.Completed
		));

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenTransferAlreadyCompleted_ShouldNotUpdateStatus()
	{
		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TransferFactory.Reconstitute(
			id: TransferId,
			fromAccountId: FromAccountId,
			toAccountId: ToAccountId,
			status: TransferStatus.Completed
		));

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _transferWriteRepository.DidNotReceive().UpdateStatusAsync(
			transferId: Arg.Any<Guid>(),
			status: Arg.Any<TransferStatus>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenTransferAlreadyCompensated_ShouldSkipWithoutSavingAccounts()
	{
		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TransferFactory.Reconstitute(
			id: TransferId,
			fromAccountId: FromAccountId,
			toAccountId: ToAccountId,
			status: TransferStatus.Compensated
		));

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenTransferAlreadyFailed_ShouldSkipWithoutSavingAccounts()
	{
		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TransferFactory.Reconstitute(
			id: TransferId,
			fromAccountId: FromAccountId,
			toAccountId: ToAccountId,
			status: TransferStatus.Failed
		));

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}