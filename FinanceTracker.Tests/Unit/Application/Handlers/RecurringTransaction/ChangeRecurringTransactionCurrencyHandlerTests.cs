using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class ChangeRecurringTransactionCurrencyHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private ChangeRecurringTransactionCurrencyHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_handler = new ChangeRecurringTransactionCurrencyHandler(
			recurringTransactionWriteRepository: _writeRepository,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCurrency_ShouldCallChangeCurrencyAsync()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction rt = RecurringTransactionFactory.Create().Value!;
		FinanceTracker.Core.ValueObjects.Currency newCurrency = FinanceTracker.Core.ValueObjects.Currency.Create(value: "EUR").Value;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionCurrencyCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, Currency: newCurrency),
			user: rt,
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
		FinanceTracker.Core.ValueObjects.Currency newCurrency = FinanceTracker.Core.ValueObjects.Currency.Create(value: "EUR").Value;

		await _handler.HandleAsync(
			command: new ChangeRecurringTransactionCurrencyCommand(UserId: rt.UserId, RecurringTransactionId: rt.Id, Currency: newCurrency),
			user: rt,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<RecurringTransactionCurrencyChangedNotification>(n =>
			n!.RecurringTransactionId == rt.Id &&
			n.UserId == rt.UserId &&
			n.NewCurrency == newCurrency
		));
	}
}
