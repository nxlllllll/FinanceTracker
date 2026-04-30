using FinanceTracker.Application.RecurringTransactions.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class DeactivateRecurringTransactionHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private DeactivateRecurringTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_handler = new DeactivateRecurringTransactionHandler(recurringTransactionWriteRepository: _writeRepository);
	}

	[Test]
	public async Task HandleAsync_WhenAlreadyInactive_ShouldNotCallRepository()
	{
		RecurringTransactionDto recurringTransaction = RecurringTransactionFactory.Create(isActive: false);

		await _handler.HandleAsync(
			command: new DeactivateRecurringTransactionCommand(UserId: recurringTransaction.UserId, RecurringTransactionId: recurringTransaction.Id),
			recurringTransaction: recurringTransaction,
			ct: CancellationToken.None
		);

		await _writeRepository.DidNotReceive().DeactivateAsync(
			recurringTransactionId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenActive_ShouldCallDeactivate()
	{
		RecurringTransactionDto recurringTransaction = RecurringTransactionFactory.Create(isActive: true);

		await _handler.HandleAsync(
			command: new DeactivateRecurringTransactionCommand(UserId: recurringTransaction.UserId, RecurringTransactionId: recurringTransaction.Id),
			recurringTransaction: recurringTransaction,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).DeactivateAsync(
			recurringTransactionId: recurringTransaction.Id, ct: Arg.Any<CancellationToken>()
		);
	}
}