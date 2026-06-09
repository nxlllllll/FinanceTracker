using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.Category;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Infrastructure.Services.Currency;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Category;

public sealed class CategoryTotalReadRepositoryTests : DatabaseFixture
{
	private CategoryTotalReadRepository _readRepository = null!;
	private CategoryTotalWriteRepository _writeRepository = null!;
	private IUserQueryRepository _userQueryRepository = null!;
	private ICurrencyConversionService _currencyConversionService = null!;
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_userQueryRepository = new UserReadRepository(context: Context);
		_currencyConversionService = new CurrencyConversionService(
			currencyRateReadRepository: new CurrencyRateReadRepository(context: Context),
			logger: Substitute.For<ILogger<CurrencyConversionService>>()
		);
		_readRepository = new CategoryTotalReadRepository(context: Context);
		_writeRepository = new CategoryTotalWriteRepository(
			context: Context,
			userQueryRepository: _userQueryRepository,
			currencyConversionService: _currencyConversionService,
			dateProvider: FakeDateProvider.Default
		);
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
	}

	[Test]
	public async Task GetTotalByCategoryAsync_WhenExists_ShouldReturnCorrectDto()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 1000m,
			occurredAt: occurredAt
		);
		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 500m,
			occurredAt: occurredAt
		);

		CategoryTotal? result = await _readRepository.GetByCategoryAsync(
			userId: userId,
			categoryId: categoryId,
			period: new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.CategoryId).IsEqualTo(expected: categoryId);
		await Assert.That(value: result.Total).IsEqualTo(expected: 1500m);
		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
		await Assert.That(value: result.Period).IsEqualTo(expected: new DateOnly(year: 2025, month: 1, day: 1));
	}

	[Test]
	public async Task GetTotalByCategoryAsync_WhenNotExists_ShouldReturnNull()
	{
		CategoryTotal? result = await _readRepository.GetByCategoryAsync(
			userId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			period: new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetTotalByCategoryAsync_ShouldNotReturnOtherUserData()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid anotherUserId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: anotherUserId);

		await _writeRepository.AddAsync(
			userId: anotherUserId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 9999m,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		CategoryTotal? result = await _readRepository.GetByCategoryAsync(
			userId: userId,
			categoryId: categoryId,
			period: new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetAllByPeriodAsync_ShouldReturnAllCategoriesForPeriod()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId1 = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда");
		Guid categoryId2 = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId1,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 1000m,
			occurredAt: occurredAt
		);
		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId2,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 2000m,
			occurredAt: occurredAt
		);

		IReadOnlyList<CategoryTotal> result = await _readRepository.GetAllByPeriodAsync(
			userId: userId,
			period: new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetAllByPeriodAsync_ShouldNotReturnOtherPeriods()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 1000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);
		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 2000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 2, day: 10, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		IReadOnlyList<CategoryTotal> result = await _readRepository.GetAllByPeriodAsync(
			userId: userId,
			period: new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result[0].Period).IsEqualTo(expected: new DateOnly(year: 2025, month: 1, day: 1));
	}

	[Test]
	public async Task GetAllByPeriodAsync_WhenNoData_ShouldReturnEmptyList()
	{
		IReadOnlyList<CategoryTotal> result = await _readRepository.GetAllByPeriodAsync(
			userId: Guid.CreateVersion7(),
			period: new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: result).IsEmpty();
	}
}