using FinanceTracker.Application.RecurringTransactions.Commands.ChangeRecurringTransactionDayOfMonth;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionDayOfMonthHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private ChangeRecurringTransactionDayOfMonthHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_handler = new ChangeRecurringTransactionDayOfMonthHandler(recurringTransactionWriteRepository: _writeRepository);
	}

	[Test]
	public async Task HandleAsync_ShouldCallChangeDayOfMonth()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = RecurringTransactionFactory.Create();

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionDayOfMonthCommand(UserId: recurringTransaction.UserId, RecurringTransactionId: recurringTransaction.Id, DayOfMonth: 20),
			recurringTransaction: recurringTransaction,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ChangeDayOfMonthAsync(
			recurringTransactionId: recurringTransaction.Id,
			dayOfMonth: 20,
			ct: Arg.Any<CancellationToken>()
		);
	}
}