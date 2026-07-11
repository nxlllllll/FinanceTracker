using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionAmountHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IPublisher _publisher = null!;
	private ChangeRecurringTransactionAmountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new ChangeRecurringTransactionAmountHandler(
			recurringTransactionWriteRepository: _writeRepository,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<ChangeRecurringTransactionAmountHandler>>()
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

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<RecurringTransactionAmountChangedNotification>(n =>
				n.RecurringTransactionId == rt.Id &&
				n.UserId == rt.UserId &&
				n.NewAmount == 500m),
			cancellationToken: Arg.Any<CancellationToken>()
		);
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

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<RecurringTransactionAmountChangedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
