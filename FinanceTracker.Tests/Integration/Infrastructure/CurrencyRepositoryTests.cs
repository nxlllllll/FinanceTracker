using FinanceTracker.Core.Dtos;
using FinanceTracker.Infrastructure.Database.Repositories;

namespace FinanceTracker.Tests.Integration.Infrastructure;

public sealed class CurrencyRepositoryTests : DatabaseFixture
{
	private CurrencyRepository _repository = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
		=> _repository = new CurrencyRepository(context: Context);

	[Test]
	public async Task GetAllAsync_WithNoCurrencies_ShouldReturnEmptyList()
	{
		IReadOnlyList<CurrencyDto> result = await _repository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task GetAllAsync_WithCurrencies_ShouldReturnAll()
	{
		await CreateCurrencyAsync(code: "RUB");
		await CreateCurrencyAsync(code: "USD");

		IReadOnlyList<CurrencyDto> result = await _repository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetByCodeAsync_WithNonExistentCode_ShouldReturnNull()
	{
		CurrencyDto? result = await _repository.GetByCodeAsync(code: "USD");

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByCodeAsync_WithExistingCode_ShouldReturnCorrectDto()
	{
		await CreateCurrencyAsync(code: "RUB");

		CurrencyDto? result = await _repository.GetByCodeAsync(code: "RUB");

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result.Code).IsEqualTo(expected: "RUB");
		await Assert.That(value: result.Name).IsEqualTo(expected: "Российский рубль");
		await Assert.That(value: result.Symbol).IsEqualTo(expected: "₽");
		await Assert.That(value: result.IsActive).IsTrue();
	}
}