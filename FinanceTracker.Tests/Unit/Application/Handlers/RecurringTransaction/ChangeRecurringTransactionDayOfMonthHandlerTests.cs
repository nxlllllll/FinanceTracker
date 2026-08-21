using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionDayOfMonthHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IUserQueryRepository _userQueryRepository = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private ChangeRecurringTransactionDayOfMonthHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_userQueryRepository = Substitute.For<IUserQueryRepository>();

		SetupTimeZone(timeZone: TimeZoneId.Utc);

		_handler = new ChangeRecurringTransactionDayOfMonthHandler(
			recurringTransactionWriteRepository: _writeRepository,
			postCommitNotifications: _postCommitNotifications,
			userQueryRepository: _userQueryRepository,
			dateProvider: FakeDateProvider.Default
		);
	}

	private void SetupTimeZone(TimeZoneId? timeZone)
	{
		_userQueryRepository.GetTimeZoneAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: timeZone);
	}

	[Test]
	public async Task HandleAsync_WithValidDay_ShouldCallChangeDayOfMonthAsync()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 20),
			recurringTransaction: rt,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ChangeDayOfMonthAsync(
			recurringTransactionId: rt.Id,
			dayOfMonth: 20,
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidDay_ShouldAdvanceTheDueInstantPastTheOldOne()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create(dayOfMonth: 5).Value!;

		DateTimeOffset before = rt.NextDueAtUtc;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 20),
			recurringTransaction: rt,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ChangeDayOfMonthAsync(
			recurringTransactionId: rt.Id,
			dayOfMonth: 20,
			nextDueAtUtc: Arg.Is<DateTimeOffset>(predicate: next => next != before),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidDay_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 20),
			recurringTransaction: rt,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<RecurringTransactionDayOfMonthChangedNotification>(n =>
			n!.RecurringTransactionId == rt.Id &&
			n.UserId == rt.UserId &&
			n.NewDayOfMonth == 20
		));
	}

	[Test]
	public async Task HandleAsync_WhenTheUserHasNoTimeZone_ShouldReturnNotFound()
	{
		SetupTimeZone(timeZone: null);

		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 20),
			recurringTransaction: rt,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();

		await _writeRepository.DidNotReceive().ChangeDayOfMonthAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			dayOfMonth: Arg.Any<int>(),
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithInvalidDay_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 0),
			recurringTransaction: rt,
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
			recurringTransaction: rt,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<RecurringTransactionDayOfMonthChangedNotification>());
	}
}
