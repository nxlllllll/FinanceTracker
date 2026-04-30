using FinanceTracker.Application.Transfers.Commands;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Tests.Unit.Helpers;
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
		_handler = new CreateTransferHandler(
			accountRepository: _accountRepository,
			transferWriteRepository: _transferWriteRepository,
			currencyConversionService: _currencyConversionService,
			unitOfWork: _unitOfWork
		);
	}

	[Test]
	public async Task HandleAsync_ShouldReturnTransferId()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateAccountWithArchivation(balance: 5000m);
		FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.CreateAccountWithArchivation(balance: 1000m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<string>(), toCurrency: Arg.Any<string>(),
			date: Arg.Any<DateOnly>(), ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		Guid result = await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: toAccount.Id),
			accounts: (fromAccount, toAccount),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNotDefault();
	}

	[Test]
	public async Task HandleAsync_ShouldSaveBothAccounts()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateAccountWithArchivation(balance: 5000m);
		FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.CreateAccountWithArchivation(balance: 1000m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<string>(), toCurrency: Arg.Any<string>(),
			date: Arg.Any<DateOnly>(), ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: toAccount.Id),
			accounts: (fromAccount, toAccount),
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Id == fromAccount.Id),
			ct: Arg.Any<CancellationToken>()
		);
		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Id == toAccount.Id),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_ShouldDebitFromAndCreditTo()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateAccountWithArchivation(balance: 5000m);
		FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.CreateAccountWithArchivation(balance: 1000m);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<string>(), toCurrency: Arg.Any<string>(),
			date: Arg.Any<DateOnly>(), ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

		await _handler.HandleAsync(
			command: CreateTransferCommandFactory.Create(userId: fromAccount.UserId, fromAccountId: fromAccount.Id, toAccountId: toAccount.Id, amount: 1000m),
			accounts: (fromAccount, toAccount),
			ct: CancellationToken.None
		);

		await Assert.That(value: fromAccount.Balance.Amount).IsEqualTo(expected: 4000m);
		await Assert.That(value: toAccount.Balance.Amount).IsEqualTo(expected: 2000m);
	}
}