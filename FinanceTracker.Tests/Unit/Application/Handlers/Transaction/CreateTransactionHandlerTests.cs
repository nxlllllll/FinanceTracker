using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class CreateTransactionHandlerTests
{
	private ITransactionCreationService _transactionCreationService = null!;
	private ICategoryReadRepository _categoryReadRepository = null!;
	private IPublisher _publisher = null!;
	private CreateTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionCreationService = Substitute.For<ITransactionCreationService>();
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();
		_publisher = Substitute.For<IPublisher>();

		CategoryReadModel category = CategoryFactory.CreateReadModel(type: CategoryType.Expense);
		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		_handler = new CreateTransactionHandler(
			transactionCreationService: _transactionCreationService,
			categoryReadRepository: _categoryReadRepository,
			publisher: _publisher,
			logger: Substitute.For<ILogger<CreateTransactionHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_ShouldDelegateToService()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create().Value!;
		CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
			userId: account.UserId,
			accountId: account.Id
		);
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(accountId: account.Id, userId: account.UserId, categoryId: command.CategoryId);

		_transactionCreationService.CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Domains.Transaction.Transaction, DomainException>.Success(value: transaction));

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: command,
			user: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: transaction.Id);
		await _transactionCreationService.Received(requiredNumberOfCalls: 1).CreateAsync(
			command: command,
			account: account,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenServiceReturnsFailure_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create().Value!;
		CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
			userId: account.UserId,
			accountId: account.Id
		);
		DomainException error = new InvalidAmountException(message: "Invalid amount.");

		_transactionCreationService.CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Domains.Transaction.Transaction, DomainException>.Failure(error: error));

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: command,
			user: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsEqualTo(expected: error);
	}

	[Test]
	public async Task HandleAsync_WhenCategoryIsArchived_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create().Value!;
		CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
			userId: account.UserId,
			accountId: account.Id
		);

		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: CategoryFactory.CreateReadModel(type: CategoryType.Expense, archived: true));

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: command,
			user: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task HandleAsync_WhenCategoryIsArchived_ShouldNotCallService()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create().Value!;
		CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
			userId: account.UserId,
			accountId: account.Id
		);

		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: CategoryFactory.CreateReadModel(type: CategoryType.Expense, archived: true));

		await _handler.HandleAsync(
			command: command,
			user: account,
			ct: CancellationToken.None
		);

		await _transactionCreationService.DidNotReceive().CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenServiceSucceeds_ShouldPublishNotificationWithTransactionData()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create().Value!;
		CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
			userId: account.UserId,
			accountId: account.Id
		);
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(accountId: account.Id, userId: account.UserId, categoryId: command.CategoryId);

		_transactionCreationService.CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Domains.Transaction.Transaction, DomainException>.Success(value: transaction));

		await _handler.HandleAsync(command: command, user: account, ct: CancellationToken.None);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<TransactionCreatedNotification>(n =>
				n.TransactionId == transaction.Id &&
				n.UserId == transaction.UserId &&
				n.AccountId == transaction.AccountId &&
				n.CategoryId == transaction.CategoryId &&
				n.OccurredAt == transaction.OccurredAt),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenServiceReturnsFailure_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create().Value!;
		CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
			userId: account.UserId,
			accountId: account.Id
		);

		_transactionCreationService.CreateAsync(
			command: Arg.Any<CreateTransactionCommand>(),
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Domains.Transaction.Transaction, DomainException>.Failure(error: new InvalidAmountException(message: "Invalid amount.")));

		await _handler.HandleAsync(command: command, user: account, ct: CancellationToken.None);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<TransactionCreatedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
