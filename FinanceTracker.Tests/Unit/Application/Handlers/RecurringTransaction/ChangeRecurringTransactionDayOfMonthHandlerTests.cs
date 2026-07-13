using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionDayOfMonthHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private ChangeRecurringTransactionDayOfMonthHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_handler = new ChangeRecurringTransactionDayOfMonthHandler(
			recurringTransactionWriteRepository: _writeRepository,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithValidDay_ShouldCallChangeDayOfMonthAsync()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 15),
			user: rt,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ChangeDayOfMonthAsync(
			recurringTransactionId: rt.Id,
			dayOfMonth: 15,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidDay_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 15),
			user: rt,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<RecurringTransactionDayOfMonthChangedNotification>(n =>
			n.RecurringTransactionId == rt.Id &&
			n.UserId == rt.UserId &&
			n.NewDayOfMonth == 15
		));
	}

	[Test]
	public async Task HandleAsync_WithInvalidDay_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 0),
			user: rt,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidDayOfMonthException>();
	}

	[Test]
	public async Task HandleAsync_WithInvalidDay_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 0),
			user: rt,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<RecurringTransactionDayOfMonthChangedNotification>());
	}
}
