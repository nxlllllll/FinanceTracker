using FinanceTracker.Application.UseCases.Transfer.Commands;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transfer;

public sealed class CreateTransferHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private ITransferWriteRepository _transferWriteRepository = null!;
	private ICurrencyConversionService _currencyConversionService = null!;
	private IUnitOfWork _unitOfWork = null!;
	private CreateTransferHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_transferWriteRepository = Substitute.For<ITransferWriteRepository>();
		_currencyConversionService = Substitute.For<ICurrencyConversionService>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
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
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<CreateTransferHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_ShouldReturnTransferId()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateWithArchivation(balance: 5000m);
		FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.CreateWithArchivation(balance: 1000m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: toAccount.Id),
			account: fromAccount,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotDefault();
	}

	[Test]
	public async Task HandleAsync_ShouldSaveOnlyFromAccount()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateWithArchivation(balance: 5000m);
		FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.CreateWithArchivation(balance: 1000m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: toAccount.Id),
			account: fromAccount,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Id == fromAccount.Id),
			ct: Arg.Any<CancellationToken>()
		);
		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Id == toAccount.Id),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_ShouldDebitFromAndCreditTo()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateWithArchivation(balance: 5000m);
		FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.CreateWithArchivation(balance: 1000m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: toAccount.Id, amount: 1000m),
			account: fromAccount,
			ct: CancellationToken.None
		);

		await Assert.That(value: fromAccount.Balance.Amount).IsEqualTo(expected: 4000m);
	}
}
