using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionAmountHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private ChangeRecurringTransactionAmountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_handler = new ChangeRecurringTransactionAmountHandler(recurringTransactionWriteRepository: _writeRepository);
	}

	[Test]
	public async Task HandleAsync_ShouldCallChangeAmount()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionAmountCommand(UserId: recurringTransaction.UserId, RecurringTransactionId: recurringTransaction.Id, Amount: 9999m),
			recurringTransaction: recurringTransaction,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ChangeAmountAsync(
			recurringTransactionId: recurringTransaction.Id,
			amount: 9999m,
			ct: Arg.Any<CancellationToken>()
		);
	}
}
