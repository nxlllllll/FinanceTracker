using FinanceTracker.Application.UseCases.Users.Queries.GetTotalBalance;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetTotalBalanceHandlerTests
{
	private IUserReadRepository _userReadRepository = null!;
	private IAccountReadRepository _accountReadRepository = null!;
	private ICurrencyConversionService _currencyConversionService = null!;
	private IDateProvider _dateProvider = null!;
	private GetTotalBalanceHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userReadRepository = Substitute.For<IUserReadRepository>();
		_accountReadRepository = Substitute.For<IAccountReadRepository>();
		_currencyConversionService = Substitute.For<ICurrencyConversionService>();
		_dateProvider = Substitute.For<IDateProvider>();
		_dateProvider.UtcNow.Returns(returnThis: FakeDateProvider.Default.UtcNow);

		_handler = new GetTotalBalanceHandler(
			userReadRepository: _userReadRepository,
			accountReadRepository: _accountReadRepository,
			currencyConversionService: _currencyConversionService,
			dateProvider: _dateProvider
		);
	}

	[Test]
	public async Task Handle_WithSingleAccountInBaseCurrency_ShouldReturnBalanceWithoutConversion()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_accountReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(), 
			isArchived: Arg.Any<bool?>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [new AccountDto(
			Id: Guid.NewGuid(),
			UserId: user.Id, 
			Name: "Main",
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			Balance: 5000m, 
			IsArchived: false,
			CreatedAt: FakeDateProvider.Default.UtcNow
		)]);

		TotalBalanceDto result = await _handler.Handle(query: new GetTotalBalanceQuery(UserId: user.Id), ct: CancellationToken.None);

		await Assert.That(value: result.Balance).IsEqualTo(expected: 5000m);
		await Assert.That(value: result.Currency.Value).IsEqualTo(expected: "RUB");
		await _currencyConversionService.DidNotReceive().GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithMultipleAccountsInBaseCurrency_ShouldSumBalances()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_accountReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(), 
			isArchived: Arg.Any<bool?>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis:
		[
			new AccountDto(Id: 
				Guid.NewGuid(), 
				UserId: user.Id, 
				Name: "Main", 
				Type: AccountType.Checking, 
				Currency: Currency.Create(value: "RUB").Value,
				Balance: 3000m, 
				IsArchived: false, 
				CreatedAt: FakeDateProvider.Default.UtcNow
			),
			new AccountDto(Id: 
				Guid.NewGuid(), 
				UserId: user.Id, 
				Name: "Savings" +
				"", Type: AccountType.Savings,
				Currency: Currency.Create(value: "RUB").Value,
				Balance: 7000m,
				IsArchived: false,
				CreatedAt: FakeDateProvider.Default.UtcNow
			)
		]);

		TotalBalanceDto result = await _handler.Handle(query: new GetTotalBalanceQuery(UserId: user.Id), ct: CancellationToken.None);

		await Assert.That(value: result.Balance).IsEqualTo(expected: 10000m);
	}

	[Test]
	public async Task Handle_WithForeignCurrencyAccount_ShouldConvertToBaseCurrency()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_accountReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(), 
			isArchived: Arg.Any<bool?>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [new AccountDto(
			Id: Guid.NewGuid(), 
			UserId: user.Id, 
			Name: "USD", 
			Type: AccountType.Checking, 
			Currency: Currency.Create(value: "USD").Value, 
			Balance: 100m, 
			IsArchived: false, 
			CreatedAt: FakeDateProvider.Default.UtcNow
		)]);

		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: 90m, IsPending: false));

		TotalBalanceDto result = await _handler.Handle(query: new GetTotalBalanceQuery(UserId: user.Id), ct: CancellationToken.None);

		await Assert.That(value: result.Balance).IsEqualTo(expected: 9000m);
	}

	[Test]
	public async Task Handle_WithNoAccounts_ShouldReturnZeroBalance()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_accountReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(), 
			isArchived: Arg.Any<bool?>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		TotalBalanceDto result = await _handler.Handle(query: new GetTotalBalanceQuery(UserId: user.Id), ct: CancellationToken.None);

		await Assert.That(value: result.Balance).IsEqualTo(expected: 0m);
	}
}