using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Services.Transactions;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Validation;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class CreateTransactionHandlerTests
{
	private ITransactionCreationService _transactionCreationService = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private CreateTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionCreationService = Substitute.For<ITransactionCreationService>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_handler = new CreateTransactionHandler(
			transactionCreationService: _transactionCreationService,
			postCommitNotifications: _postCommitNotifications
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
			account: account,
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
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsEqualTo(expected: error);
	}

	[Test]
	public async Task HandleAsync_WhenServiceSucceeds_ShouldStageNotificationWithTransactionData()
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

		await _handler.HandleAsync(command: command, account: account, ct: CancellationToken.None);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<TransactionCreatedNotification>(n =>
			n!.TransactionId == transaction.Id &&
			n.UserId == transaction.UserId &&
			n.AccountId == transaction.AccountId &&
			n.CategoryId == transaction.CategoryId &&
			n.OccurredAt == transaction.OccurredAt
		));
	}

	[Test]
	public async Task HandleAsync_WhenServiceReturnsFailure_ShouldNotStageNotification()
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

		await _handler.HandleAsync(command: command, account: account, ct: CancellationToken.None);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<TransactionCreatedNotification>());
	}
}
