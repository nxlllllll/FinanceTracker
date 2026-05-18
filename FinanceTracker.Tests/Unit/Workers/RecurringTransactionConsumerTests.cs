using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transactions.Services;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.ProcessedMessage;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.RecurringTransactionProjection.Consumers;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class RecurringTransactionConsumerTests : DatabaseFixture
{
	private IAccountRepository _accountRepository = null!;
	private ITransactionCreationService _transactionCreationService = null!;
	private IRecurringTransactionReadRepository _recurringTransactionReadRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private RecurringTransactionConsumer _consumer = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_transactionCreationService = Substitute.For<ITransactionCreationService>();
		_recurringTransactionReadRepository = Substitute.For<IRecurringTransactionReadRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: call => call.Arg<Func<Task>>()());

		_consumer = new RecurringTransactionConsumer(
			accountRepository: _accountRepository,
			transactionCreationService: _transactionCreationService,
			recurringTransactionReadRepository: _recurringTransactionReadRepository,
			processedMessageReadRepository: new ProcessedMessageReadRepository(context: Context),
			processedMessageWriteRepository: new ProcessedMessageWriteRepository(context: Context),
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<RecurringTransactionConsumer>>()
		);
	}

	private void SetupValidDependencies()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.Create().Value!);

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create().Value!);

		_transactionCreationService.CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<Guid, DomainException>.Success(value: Guid.CreateVersion7()));
	}

	private static RecurringTransactionTriggeredMessage BuildMessage(Guid? messageId = null)
	{
		return new RecurringTransactionTriggeredMessage(
			MessageId: messageId ?? Guid.CreateVersion7(),
			RecurringTransactionId: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 5000m,
			Currency: "RUB",
			Direction: "Credit",
			Description: "Зарплата",
			OccurredAt: FakeDateProvider.Default.UtcNow,
			CorrelationId: Guid.CreateVersion7()
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

		int countBefore = Context.ProcessedMessages.Count();

		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		await Assert.That(value: Context.ProcessedMessages.Count()).IsEqualTo(expected: countBefore);
	}

	[Test]
	public async Task HandleAsync_WhenRecurringTransactionNotFound_ShouldNotCreateTransaction()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction?)null);

		await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

		await _transactionCreationService.DidNotReceive().CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAccountNotFound_ShouldThrow()
	{
		_recurringTransactionReadRepository.GetByIdAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RecurringTransactionFactory.Create().Value!);

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.Account.Account?)null);

		await Assert.ThrowsAsync<NotFoundException>(action: async () => await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None));
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
	public async Task HandleAsync_WhenValid_ShouldSaveProcessedMessage()
	{
		SetupValidDependencies();

		Guid messageId = Guid.CreateVersion7();
		await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

		bool saved = Context.ProcessedMessages.Any(predicate: m => m.MessageId == messageId && m.ConsumerType == nameof(RecurringTransactionConsumer));

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
			executedAt: Arg.Any<DateTime>(),
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