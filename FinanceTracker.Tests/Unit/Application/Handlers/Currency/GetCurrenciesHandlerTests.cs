using FinanceTracker.Application.UseCases.Currency.Queries.GetCurrencies;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Results;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Currency;

public sealed class GetCurrenciesHandlerTests
{
	private ICurrencyReadRepository _currencyReadRepository = null!;
	private GetCurrenciesHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_currencyReadRepository = Substitute.For<ICurrencyReadRepository>();
		_handler = new GetCurrenciesHandler(currencyReadRepository: _currencyReadRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnSuccessWithCurrenciesFromRepository()
	{
		IReadOnlyList<CurrencyInfo> currencies =
		[
			new CurrencyInfo(Code: "USD", Name: "US Dollar", Symbol: "$", IsActive: true),
			new CurrencyInfo(Code: "RUB", Name: "Russian Ruble", Symbol: "₽", IsActive: true)
		];

		_currencyReadRepository.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: currencies);

		Result<IReadOnlyList<CurrencyInfo>, AppException> result = await _handler.Handle(query: new GetCurrenciesQuery(), ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEquivalentTo(expected: currencies);
	}

	[Test]
	public async Task Handle_WhenRepositoryReturnsEmptyList_ShouldReturnSuccessWithEmptyList()
	{
		_currencyReadRepository.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: []);

		Result<IReadOnlyList<CurrencyInfo>, AppException> result = await _handler.Handle(query: new GetCurrenciesQuery(), ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEmpty();
	}

	[Test]
	public async Task Handle_ShouldPassCancellationTokenToRepository()
	{
		using CancellationTokenSource cts = new CancellationTokenSource();
		_currencyReadRepository.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: []);

		await _handler.Handle(query: new GetCurrenciesQuery(), ct: cts.Token);

		await _currencyReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(ct: cts.Token);
	}
}
