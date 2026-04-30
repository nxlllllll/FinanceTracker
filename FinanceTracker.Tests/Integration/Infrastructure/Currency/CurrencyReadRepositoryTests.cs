using FinanceTracker.Core.Dtos;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

namespace FinanceTracker.Tests.Integration.Infrastructure.Currency;

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

	[Test]
	public async Task GetAllAsync_WithNoCurrencies_ShouldReturnEmptyList()
	{
		IReadOnlyList<CurrencyDto> result = await _readRepository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task GetAllAsync_WithCurrencies_ShouldReturnAll()
	{
		await _currencyBuilder.CreateAsync(code: "RUB");
		await _currencyBuilder.CreateAsync(code: "USD");

		IReadOnlyList<CurrencyDto> result = await _readRepository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetByCodeAsync_WithNonExistentCode_ShouldReturnNull()
	{
		CurrencyDto? result = await _readRepository.GetByCodeAsync(code: "USD");

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByCodeAsync_WithExistingCode_ShouldReturnCorrectDto()
	{
		await _currencyBuilder.CreateAsync(code: "RUB");

		CurrencyDto? result = await _readRepository.GetByCodeAsync(code: "RUB");

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result.Code).IsEqualTo(expected: "RUB");
		await Assert.That(value: result.Name).IsEqualTo(expected: "Российский рубль");
		await Assert.That(value: result.Symbol).IsEqualTo(expected: "₽");
		await Assert.That(value: result.IsActive).IsTrue();
	}
}