using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ActivateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ActivateRecurringTransactionHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private ActivateRecurringTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_handler = new ActivateRecurringTransactionHandler(
			recurringTransactionWriteRepository: _writeRepository,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WhenAlreadyActive_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create(isActive: true).Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ActivateRecurringTransactionCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id),
			user: rt,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ActivatingException>();
	}

	[Test]
	public async Task HandleAsync_WhenAlreadyActive_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create(isActive: true).Value!;

		await _handler.HandleAsync(
			command: new ActivateRecurringTransactionCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id),
			user: rt,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<RecurringTransactionActivatedNotification>());
	}

	[Test]
	public async Task HandleAsync_WhenInactive_ShouldCallActivate()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create(isActive: false).Value!;

		await _handler.HandleAsync(
			command: new ActivateRecurringTransactionCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id),
			user: rt,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ActivateAsync(
			recurringTransactionId: rt.Id,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenInactive_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create(isActive: false).Value!;

		await _handler.HandleAsync(
			command: new ActivateRecurringTransactionCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id),
			user: rt,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<RecurringTransactionActivatedNotification>(n =>
			n.RecurringTransactionId == rt.Id &&
			n.UserId == rt.UserId
		));
	}
}
