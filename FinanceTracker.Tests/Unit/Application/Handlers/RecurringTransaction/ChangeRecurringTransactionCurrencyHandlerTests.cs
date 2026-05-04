using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionCurrencyHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private ChangeRecurringTransactionCurrencyHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_handler = new ChangeRecurringTransactionCurrencyHandler(recurringTransactionWriteRepository: _writeRepository);
	}

	[Test]
	public async Task HandleAsync_ShouldCallChangeCurrency()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = RecurringTransactionFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionCurrencyCommand(UserId: recurringTransaction.UserId, RecurringTransactionId: recurringTransaction.Id, Currency: "USD"),
			recurringTransaction: recurringTransaction,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ChangeCurrencyAsync(
			recurringTransactionId: recurringTransaction.Id,
			currency: "USD",
			ct: Arg.Any<CancellationToken>()
		);
	}
}