using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionCurrencyHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IPublisher _publisher = null!;
	private ChangeRecurringTransactionCurrencyHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new ChangeRecurringTransactionCurrencyHandler(
			recurringTransactionWriteRepository: _writeRepository,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<ChangeRecurringTransactionCurrencyHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCurrency_ShouldCallChangeCurrencyAsync()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;
		Currency newCurrency = Currency.Create(value: "EUR").Value;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionCurrencyCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, Currency: newCurrency),
			entity: rt,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).ChangeCurrencyAsync(
			recurringTransactionId: rt.Id,
			currency: newCurrency,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCurrency_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;
		Currency newCurrency = Currency.Create(value: "EUR").Value;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionCurrencyCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, Currency: newCurrency),
			entity: rt,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<RecurringTransactionCurrencyChangedNotification>(n =>
				n.RecurringTransactionId == rt.Id &&
				n.UserId == rt.UserId &&
				n.NewCurrency == newCurrency),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
