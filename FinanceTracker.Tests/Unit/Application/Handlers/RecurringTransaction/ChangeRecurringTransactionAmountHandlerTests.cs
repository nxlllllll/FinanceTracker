using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionAmountHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private ChangeRecurringTransactionAmountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_handler = new ChangeRecurringTransactionAmountHandler(
			recurringTransactionWriteRepository: _writeRepository,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithValidAmount_ShouldCallChangeAmountAsync()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionAmountCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, Amount: 500m),
			user: rt,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ChangeAmountAsync(
			recurringTransactionId: rt.Id,
			amount: 500m,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidAmount_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionAmountCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, Amount: 500m),
			user: rt,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<RecurringTransactionAmountChangedNotification>(n =>
			n.RecurringTransactionId == rt.Id &&
			n.UserId == rt.UserId &&
			n.NewAmount == 500m
		));
	}

	[Test]
	public async Task HandleAsync_WithInvalidAmount_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeRecurringTransactionAmountCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, Amount: -1m),
			user: rt,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WithInvalidAmount_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionAmountCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, Amount: -1m),
			user: rt,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<RecurringTransactionAmountChangedNotification>());
	}
}
