using FinanceTracker.Application.Services.Transactions;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.ProcessedMessage;
using FinanceTracker.Infrastructure.Database.Repositories.ProcessedMessage;
using FinanceTracker.Infrastructure.Database.Repositories.UnresolvableEvent;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.RecurringTransactionProjection.Consumer;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class RecurringTransactionConsumerTests : DatabaseFixture
{
	private IAccountRepository _accountRepository = null!;
	private ITransactionCreationService _transactionCreationService = null!;
	private IRecurringTransactionReadRepository _recurringTransactionReadRepository = null!;
	private RecurringTransactionConsumer _consumer = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_transactionCreationService = Substitute.For<ITransactionCreationService>();
		_recurringTransactionReadRepository = Substitute.For<IRecurringTransactionReadRepository>();

		_consumer = new RecurringTransactionConsumer(
			accountRepository: _accountRepository,
			transactionCreationService: _transactionCreationService,
			recurringTransactionReadRepository: _recurringTransactionReadRepository,
			processedMessageReadRepository: new ProcessedMessageReadRepository(context: Context),
			processedMessageWriteRepository: new ProcessedMessageWriteRepository(context: Context),
			unresolvableEventWriteRepository: new UnresolvableEventWriteRepository(context: Context),
			unitOfWork: UnitOfWork,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<RecurringTransactionConsumer>>()
		);
	}

	private void SetupValidDependencies()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.CreateReadModel());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		_transactionCreationService.CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<Transaction, DomainException>.Success(value: TransactionFactory.Create()));
	}

	private static RecurringTransactionTriggeredMessage BuildMessage(
		Guid? messageId = null,
		Guid? recurringTransactionId = null,
		string currency = "RUB",
		string direction = "Credit")
	{
		return new RecurringTransactionTriggeredMessage(
			MessageId: messageId ?? Guid.CreateVersion7(),
			RecurringTransactionId: recurringTransactionId ?? Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 5000m,
			Currency: currency,
			Direction: direction,
			Description: "Зарплата",
			OccurredAt: FakeDateProvider.Default.UtcNow,
			CorrelationId: Guid.CreateVersion7()
		);
	}

	private async Task<bool> HasUnresolvableEventAsync(Guid referenceId)
	{
		return await Context.UnresolvableEvents.AnyAsync(
			predicate: e => e.ReferenceId == referenceId && e.Type == UnresolvableEventType.RecurringTransactionFailed
		);
	}

	[Test]
	public async Task HandleAsync_WhenMessageAlreadyProcessed_ShouldNotCreateTransaction()
	{
		Guid messageId = Guid.CreateVersion7();

		await Context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
		{
			MessageId = messageId,
			ConsumerType = nameof(RecurringTransactionConsumer),
			ProcessedAt = FakeDateProvider.Default.UtcNow
		});
		await Context.SaveChangesAsync();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		await _transactionCreationService.DidNotReceive().CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
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
			ConsumerType = nameof(RecurringTransactionConsumer),
			ProcessedAt = FakeDateProvider.Default.UtcNow
		});
		await Context.SaveChangesAsync();

		int countBefore = await Context.ProcessedMessages.CountAsync();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		int countAfter = await Context.ProcessedMessages.CountAsync();

		await Assert.That(value: countAfter).IsEqualTo(expected: countBefore);
	}

	[Test]
	public async Task HandleAsync_WhenRecurringTransactionNotFound_ShouldNotCreateTransaction()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (RecurringTransactionReadModel?)null);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _transactionCreationService.DidNotReceive().CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenRecurringTransactionNotFound_ShouldEscalateToUnresolvableEvents()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (RecurringTransactionReadModel?)null);

		Guid recurringTransactionId = Guid.CreateVersion7();
		await _consumer.HandleAsync(message: BuildMessage(recurringTransactionId: recurringTransactionId), ct: CancellationToken.None);

		await Assert.That(value: await HasUnresolvableEventAsync(referenceId: recurringTransactionId)).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenAccountNotFound_ShouldNotCreateTransaction()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.CreateReadModel());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.Account.Account?)null);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _transactionCreationService.DidNotReceive().CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAccountNotFound_ShouldMarkMessageAsProcessed()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.CreateReadModel());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.Account.Account?)null);

		RecurringTransactionTriggeredMessage message = BuildMessage();
		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

		bool isProcessed = await Context.ProcessedMessages.AnyAsync(
			predicate: p => p.MessageId == message.MessageId && p.ConsumerType == nameof(RecurringTransactionConsumer)
		);
		await Assert.That(value: isProcessed).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenAccountNotFound_ShouldEscalateToUnresolvableEvents()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.CreateReadModel());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.Account.Account?)null);

		Guid recurringTransactionId = Guid.CreateVersion7();
		await _consumer.HandleAsync(message: BuildMessage(recurringTransactionId: recurringTransactionId), ct: CancellationToken.None);

		await Assert.That(value: await HasUnresolvableEventAsync(referenceId: recurringTransactionId)).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenCurrencyInvalid_ShouldNotCreateTransaction()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.CreateReadModel());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		await _consumer.HandleAsync(message: BuildMessage(currency: "NOT_A_CURRENCY"), ct: CancellationToken.None);

		await _transactionCreationService.DidNotReceive().CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCurrencyInvalid_ShouldEscalateToUnresolvableEvents()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.CreateReadModel());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		Guid recurringTransactionId = Guid.CreateVersion7();
		await _consumer.HandleAsync(
			message: BuildMessage(recurringTransactionId: recurringTransactionId, currency: "NOT_A_CURRENCY"),
			ct: CancellationToken.None
		);

		await Assert.That(value: await HasUnresolvableEventAsync(referenceId: recurringTransactionId)).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenDirectionInvalid_ShouldNotCreateTransaction()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.CreateReadModel());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		await _consumer.HandleAsync(message: BuildMessage(direction: "Sideways"), ct: CancellationToken.None);

		await _transactionCreationService.DidNotReceive().CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenDirectionInvalid_ShouldEscalateToUnresolvableEvents()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.CreateReadModel());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		Guid recurringTransactionId = Guid.CreateVersion7();
		await _consumer.HandleAsync(
			message: BuildMessage(recurringTransactionId: recurringTransactionId, direction: "Sideways"),
			ct: CancellationToken.None
		);

		await Assert.That(value: await HasUnresolvableEventAsync(referenceId: recurringTransactionId)).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenTransactionCreationFails_ShouldMarkMessageAsProcessed()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.CreateReadModel());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		_transactionCreationService.CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<Transaction, DomainException>.Failure(error: new InsufficientFundsException(
			message: "Insufficient funds.",
			balance: Money.Reconstitute(amount: 0m, currency: Currency.Reconstitute(value: "RUB"))
		)));

		RecurringTransactionTriggeredMessage message = BuildMessage();
		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

		bool isProcessed = await Context.ProcessedMessages.AnyAsync(
			predicate: p => p.MessageId == message.MessageId && p.ConsumerType == nameof(RecurringTransactionConsumer)
		);
		await Assert.That(value: isProcessed).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenTransactionCreationFails_ShouldEscalateToUnresolvableEventsWithFailureReason()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.CreateReadModel());

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		const string failureMessage = "Insufficient funds.";
		_transactionCreationService.CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<Transaction, DomainException>.Failure(error: new InsufficientFundsException(
			message: failureMessage,
			balance: Money.Reconstitute(amount: 0m, currency: Currency.Reconstitute(value: "RUB"))
		)));

		Guid recurringTransactionId = Guid.CreateVersion7();
		await _consumer.HandleAsync(message: BuildMessage(recurringTransactionId: recurringTransactionId), ct: CancellationToken.None);

		UnresolvableEventType type = Context.UnresolvableEvents.Single(predicate: e => e.ReferenceId == recurringTransactionId).Type;
		string reason = Context.UnresolvableEvents.Single(predicate: e => e.ReferenceId == recurringTransactionId).Reason;

		await Assert.That(value: type).IsEqualTo(expected: UnresolvableEventType.RecurringTransactionFailed);
		await Assert.That(value: reason).IsEqualTo(expected: failureMessage);
	}

	[Test]
	public async Task HandleAsync_WhenValid_ShouldCreateTransaction()
	{
		SetupValidDependencies();

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _transactionCreationService.Received(requiredNumberOfCalls: 1).CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenValid_ShouldNotEscalateToUnresolvableEvents()
	{
		SetupValidDependencies();

		Guid recurringTransactionId = Guid.CreateVersion7();
		await _consumer.HandleAsync(message: BuildMessage(recurringTransactionId: recurringTransactionId), ct: CancellationToken.None);

		await Assert.That(value: await HasUnresolvableEventAsync(referenceId: recurringTransactionId)).IsFalse();
	}

	[Test]
	public async Task HandleAsync_WhenValid_ShouldSaveProcessedMessage()
	{
		SetupValidDependencies();

		Guid messageId = Guid.CreateVersion7();
		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		bool saved = await Context.ProcessedMessages.AnyAsync(predicate: m =>
			m.MessageId == messageId &&
			m.ConsumerType == nameof(RecurringTransactionConsumer)
		);

		await Assert.That(value: saved).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenValid_ShouldNotCallMarkExecuted()
	{
		IRecurringTransactionWriteRepository writeRepo = Substitute.For<IRecurringTransactionWriteRepository>();

		SetupValidDependencies();

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await writeRepo.DidNotReceive().MarkExecutedAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			executedAt: Arg.Any<DateTimeOffset>(),
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCalledTwiceWithSameId_ShouldCreateTransactionOnce()
	{
		SetupValidDependencies();

		RecurringTransactionTriggeredMessage message = BuildMessage();

		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);
		await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

		await _transactionCreationService.Received(requiredNumberOfCalls: 1).CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RecurringTransactionConsumer_ShouldImplement_IMessageHandler()
		=> await Assert.That(value: _consumer is IMessageHandler<RecurringTransactionTriggeredMessage> result).IsTrue();
}
