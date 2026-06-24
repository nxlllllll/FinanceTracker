using FinanceTracker.Application.UseCases.Transfer.Authorization;
using FinanceTracker.Application.UseCases.Transfer.Commands;
using FinanceTracker.Application.UseCases.Transfer.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transfer;

public sealed class CreateTransferHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private ITransferWriteRepository _transferWriteRepository = null!;
	private ICurrencyConversionService _currencyConversionService = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPublisher _publisher = null!;
	private CreateTransferHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_transferWriteRepository = Substitute.For<ITransferWriteRepository>();
		_currencyConversionService = Substitute.For<ICurrencyConversionService>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_publisher = Substitute.For<IPublisher>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());

		_handler = new CreateTransferHandler(
			accountRepository: _accountRepository,
			transferWriteRepository: _transferWriteRepository,
			currencyConversionService: _currencyConversionService,
			unitOfWork: _unitOfWork,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<CreateTransferHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_ShouldReturnTransferId()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateWithArchivation(balance: 5000m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: Guid.CreateVersion7()),
			accounts: new TransferAccounts(FromAccount: fromAccount, ToAccountCurrency: fromAccount.Currency),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotDefault();
	}

	[Test]
	public async Task HandleAsync_ShouldSaveFromAccount()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateWithArchivation(balance: 5000m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: Guid.CreateVersion7()),
			accounts: new TransferAccounts(FromAccount: fromAccount, ToAccountCurrency: fromAccount.Currency),
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(a => a.Id == fromAccount.Id),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_ShouldDebitFromAccount()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateWithArchivation(balance: 5000m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: Guid.CreateVersion7(), amount: 1000m),
			accounts: new TransferAccounts(FromAccount: fromAccount, ToAccountCurrency: fromAccount.Currency),
			ct: CancellationToken.None
		);

		await Assert.That(value: fromAccount.Balance.Amount).IsEqualTo(expected: 4000m);
	}

	[Test]
	public async Task HandleAsync_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateWithArchivation(balance: 5000m);
		Guid toAccountId = Guid.CreateVersion7();

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: toAccountId),
			accounts: new TransferAccounts(FromAccount: fromAccount, ToAccountCurrency: fromAccount.Currency),
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<TransferCreatedNotification>(n =>
				n.UserId == fromAccount.UserId &&
				n.FromAccountId == fromAccount.Id &&
				n.ToAccountId == toAccountId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenInsufficientFunds_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateWithArchivation(balance: 100m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: Guid.CreateVersion7(), amount: 9999m),
			accounts: new TransferAccounts(FromAccount: fromAccount, ToAccountCurrency: fromAccount.Currency),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenInsufficientFunds_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateWithArchivation(balance: 100m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: Guid.CreateVersion7(), amount: 9999m),
			accounts: new TransferAccounts(FromAccount: fromAccount, ToAccountCurrency: fromAccount.Currency),
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<TransferCreatedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithDifferentAccountCurrencies_ShouldRequestConversionUsingRealAccountCurrencies()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.Create(balance: 5000m, currency: "USD").Value!;
		fromAccount.ClearEvents();
		Currency toAccountCurrency = Currency.Create(value: "EUR").Value!;

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 0.9m, IsPending: false));

		await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: Guid.CreateVersion7()),
			accounts: new TransferAccounts(FromAccount: fromAccount, ToAccountCurrency: toAccountCurrency),
			ct: CancellationToken.None
		);

		await _currencyConversionService.Received(requiredNumberOfCalls: 1).GetConversionRateAsync(
			fromCurrency: fromAccount.Currency,
			toCurrency: toAccountCurrency,
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}