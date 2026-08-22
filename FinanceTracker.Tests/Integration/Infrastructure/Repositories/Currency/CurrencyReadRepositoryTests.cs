using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Currency;

public sealed class CurrencyReadRepositoryTests : DatabaseFixture
{
	private CurrencyReadRepository _readRepository = null!;
	private CurrencyBuilder _currencyBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
	{
		_readRepository = new CurrencyReadRepository(context: Context);
		_currencyBuilder = new CurrencyBuilder(context: Context);
	}

	private async Task ClearCurrenciesAsync()
	   => await Context.Currencies.ExecuteDeleteAsync();

	[Test]
	public async Task GetAllAsync_WithNoCurrencies_ShouldReturnEmptyList()
	{
		await ClearCurrenciesAsync();

		IReadOnlyList<CurrencyInfo> result = await _readRepository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task GetAllAsync_WithCurrencies_ShouldReturnAll()
	{
		await ClearCurrenciesAsync();
		await _currencyBuilder.CreateAsync(code: "RUB");
		await _currencyBuilder.CreateAsync(code: "USD");

		IReadOnlyList<CurrencyInfo> result = await _readRepository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetByCodeAsync_WithNonExistentCode_ShouldReturnNull()
	{
		await ClearCurrenciesAsync();

		CurrencyInfo? result = await _readRepository.GetByCodeAsync(code: "USD");

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByCodeAsync_WithExistingCode_ShouldReturnCorrectDto()
	{
		await ClearCurrenciesAsync();

		await _currencyBuilder.CreateAsync(code: "RUB");

		CurrencyInfo? result = await _readRepository.GetByCodeAsync(code: "RUB");

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result.Code).IsEqualTo(expected: "RUB");
		await Assert.That(value: result.Name).IsEqualTo(expected: "Российский рубль");
		await Assert.That(value: result.Symbol).IsEqualTo(expected: "₽");
		await Assert.That(value: result.IsActive).IsTrue();
	}
}
