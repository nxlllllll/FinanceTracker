using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionDayOfMonthHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IPublisher _publisher = null!;
	private ChangeRecurringTransactionDayOfMonthHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new ChangeRecurringTransactionDayOfMonthHandler(
			recurringTransactionWriteRepository: _writeRepository,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithValidDay_ShouldCallChangeDayOfMonthAsync()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 15),
			entity: rt,
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
			entity: rt,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<RecurringTransactionDayOfMonthChangedNotification>(n =>
				n.RecurringTransactionId == rt.Id &&
				n.UserId == rt.UserId &&
				n.NewDayOfMonth == 15),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithInvalidDay_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, DayOfMonth: 0),
			entity: rt,
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
			entity: rt,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<RecurringTransactionDayOfMonthChangedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}