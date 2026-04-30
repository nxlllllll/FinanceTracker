using FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ActivateRecurringTransactionHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private ActivateRecurringTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_handler = new ActivateRecurringTransactionHandler(recurringTransactionWriteRepository: _writeRepository);
	}

	[Test]
	public async Task HandleAsync_WhenAlreadyActive_ShouldNotCallRepository()
	{
		RecurringTransactionDto recurringTransaction = RecurringTransactionFactory.Create(isActive: true);

		await _handler.HandleAsync(
			command: new ActivateRecurringTransactionCommand(UserId: recurringTransaction.UserId, RecurringTransactionId: recurringTransaction.Id),
			recurringTransaction: recurringTransaction,
			ct: CancellationToken.None
		);

		await _writeRepository.DidNotReceive().ActivateAsync(
			recurringTransactionId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenInactive_ShouldCallActivate()
	{
		RecurringTransactionDto recurringTransaction = RecurringTransactionFactory.Create(isActive: false);

		await _handler.HandleAsync(
			command: new ActivateRecurringTransactionCommand(UserId: recurringTransaction.UserId, RecurringTransactionId: recurringTransaction.Id),
			recurringTransaction: recurringTransaction,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ActivateAsync(
			recurringTransactionId: recurringTransaction.Id, ct: Arg.Any<CancellationToken>()
		);
	}
}