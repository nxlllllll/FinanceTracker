using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.DeactivateRecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
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
	public async Task HandleAsync_WhenAlreadyInactive_ShouldThrowDeactivatingException()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = RecurringTransactionFactory.Create(isActive: false).Value!;
		
		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new DeactivateRecurringTransactionCommand(UserId: recurringTransaction.UserId, RecurringTransactionId: recurringTransaction.Id),
			recurringTransaction: recurringTransaction,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<DeactivatingException>();
	}

	[Test]
	public async Task HandleAsync_WhenActive_ShouldCallDeactivate()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = RecurringTransactionFactory.Create(isActive: true).Value!;

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