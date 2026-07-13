using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionDescription;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ChangeTransactionDescriptionHandlerTests
{
	private ITransactionWriteRepository _transactionWriteRepository = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private ChangeTransactionDescriptionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_handler = new ChangeTransactionDescriptionHandler(
			transactionWriteRepository: _transactionWriteRepository,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WhenDescriptionChanges_ShouldCallRepository()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(description: "Old description");

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeTransactionDescriptionCommand(
				UserId: transaction.UserId,
				TransactionId: transaction.Id,
				Description: "New description"
			),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).ChangeDescriptionAsync(
			transactionId: transaction.Id,
			userId: transaction.UserId,
			description: "New description",
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenDescriptionChanges_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(description: "Old description");

		await _handler.HandleAsync(
			command: new ChangeTransactionDescriptionCommand(
				UserId: transaction.UserId,
				TransactionId: transaction.Id,
				Description: "New description"
			),
			transaction: transaction,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<TransactionDescriptionChangedNotification>(n =>
			n.TransactionId == transaction.Id &&
			n.UserId == transaction.UserId &&
			n.OldDescription == "Old description" &&
			n.NewDescription == "New description"
		));
	}

	[Test]
	public async Task HandleAsync_WhenDescriptionIsSame_ShouldNotCallRepository()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(description: "Same description");

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeTransactionDescriptionCommand(
				UserId: transaction.UserId,
				TransactionId: transaction.Id,
				Description: "Same description"
			),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _transactionWriteRepository.DidNotReceive().ChangeDescriptionAsync(
			transactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			description: Arg.Any<string?>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenDescriptionIsSame_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(description: "Same description");

		await _handler.HandleAsync(
			command: new ChangeTransactionDescriptionCommand(
				UserId: transaction.UserId,
				TransactionId: transaction.Id,
				Description: "Same description"
			),
			transaction: transaction,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<TransactionDescriptionChangedNotification>());
	}

	[Test]
	public async Task HandleAsync_ShouldReturnTransactionId()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create();

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeTransactionDescriptionCommand(
				UserId: transaction.UserId,
				TransactionId: transaction.Id,
				Description: "New description"
			),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Value).IsEqualTo(expected: transaction.Id);
	}
}
