using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.Category;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Infrastructure.Services.Currency;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Category;

public sealed class CategoryTotalWriteRepositoryTests : DatabaseFixture
{
	private CategoryTotalWriteRepository _writeRepository = null!;
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private CurrencyBuilder _currencyBuilder = null!;
	private IUserQueryRepository _userQueryRepository = null!;
	private ICurrencyConversionService _currencyConversionService = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_userQueryRepository = new UserReadRepository(context: Context);
		_currencyConversionService = new CurrencyConversionService(
			currencyRateReadRepository: new CurrencyRateReadRepository(context: Context),
			logger: Substitute.For<ILogger<CurrencyConversionService>>()
		);

		_writeRepository = new CategoryTotalWriteRepository(
			context: Context,
			userQueryRepository: _userQueryRepository,
			currencyConversionService: _currencyConversionService,
			dateProvider: FakeDateProvider.Default
		);
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_currencyBuilder = new CurrencyBuilder(context: Context);
	}

	private async Task SeedRateAsync(
		Core.ValueObjects.Currency baseCode,
		Core.ValueObjects.Currency targetCode,
		decimal rate,
		DateOnly date,
		DateTimeOffset createdAt)
	{
		await Context.CurrencyRates.AddAsync(entity: new CurrencyRateEntity()
		{
			BaseCode = baseCode,
			TargetCode = targetCode,
			Rate = rate,
			ActualAt = date,
			CreatedAt = createdAt
		});
		await Context.SaveChangesAsync();
	}

	[Test]
	public async Task AddAsync_WhenNoRecordExists_ShouldCreateNewRecord()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			amount: 1000m,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		CategoryTotalEntity? entity = await Context.CategoryTotals.FirstOrDefaultAsync(predicate: ct =>
			ct.UserId == userId &&
			ct.CategoryId == categoryId &&
			ct.Period == new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: entity).IsNotNull();
		await Assert.That(value: entity!.Total).IsEqualTo(expected: 1000m);
		await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task AddAsync_WhenRecordExists_ShouldAccumulateTotal()
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

		CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
		);

		await Assert.That(value: entity.Total).IsEqualTo(expected: 1500m);
		await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task SubtractAsync_ShouldDecreaseTotal()
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
		await _writeRepository.SubtractAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 400m,
			occurredAt: occurredAt
		);

		CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
		);

		await Assert.That(value: entity.Total).IsEqualTo(expected: 600m);
		await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task AddAsync_ShouldSeparatePeriodsByMonth()
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

		int count = await Context.CategoryTotals.CountAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
		);

		await Assert.That(value: count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task ChangeCategoryAsync_ShouldSubtractFromOldAndAddToNew()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid oldCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда");
		Guid newCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: oldCategoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 1000m,
			occurredAt: occurredAt
		);
		await _writeRepository.ChangeCategoryAsync(
			userId: userId,
			oldCategoryId: oldCategoryId,
			newCategoryId: newCategoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 1000m,
			occurredAt: occurredAt
		);

		CategoryTotalEntity oldEntity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == oldCategoryId
		);
		CategoryTotalEntity newEntity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == newCategoryId
		);

		await Assert.That(value: oldEntity.Total).IsEqualTo(expected: 0m);
		await Assert.That(value: newEntity.Total).IsEqualTo(expected: 1000m);
	}

	[Test]
	public async Task AddThenSubtract_WhenANewerRateAppearsInBetween_ShouldStillNetToZero()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		await _currencyBuilder.CreateAsync(code: "USD");

		Core.ValueObjects.Currency usd = Core.ValueObjects.Currency.Create(value: "USD").Value;
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;

		DateTimeOffset transactionOccurredAt = new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 9, minute: 0, second: 0, offset: TimeSpan.Zero);
		
		await SeedRateAsync(
			baseCode: usd,
			targetCode: rub,
			rate: 90m,
			date: new DateOnly(year: 2025, month: 1, day: 9),
			createdAt: new DateTimeOffset(year: 2025, month: 1, day: 9, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: usd,
			amount: 100m,
			occurredAt: transactionOccurredAt
		);

		await SeedRateAsync(
			baseCode: usd,
			targetCode: rub,
			rate: 95m,
			date: new DateOnly(year: 2025, month: 1, day: 10),
			createdAt: new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.SubtractAsync(
			userId: userId,
			categoryId: categoryId,
			currency: usd,
			amount: 100m,
			occurredAt: transactionOccurredAt
		);

		CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
		);

		await Assert.That(value: entity.Total).IsEqualTo(expected: 0m);
		await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task ChangeCategoryAsync_WhenANewerRateAppearsInBetween_ShouldStillMoveExactAmount()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid oldCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда");
		Guid newCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
		await _currencyBuilder.CreateAsync(code: "USD");

		Core.ValueObjects.Currency usd = Core.ValueObjects.Currency.Create(value: "USD").Value;
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;

		DateTimeOffset transactionOccurredAt = new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 9, minute: 0, second: 0, offset: TimeSpan.Zero);

		await SeedRateAsync(
			baseCode: usd,
			targetCode: rub,
			rate: 90m,
			date: new DateOnly(year: 2025, month: 1, day: 9),
			createdAt: new DateTimeOffset(year: 2025, month: 1, day: 9, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: oldCategoryId,
			currency: usd,
			amount: 100m,
			occurredAt: transactionOccurredAt
		);

		await SeedRateAsync(
			baseCode: usd,
			targetCode: rub,
			rate: 95m,
			date: new DateOnly(year: 2025, month: 1, day: 10),
			createdAt: new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.ChangeCategoryAsync(
			userId: userId,
			oldCategoryId: oldCategoryId,
			newCategoryId: newCategoryId,
			currency: usd,
			amount: 100m,
			occurredAt: transactionOccurredAt
		);

		CategoryTotalEntity oldEntity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == oldCategoryId
		);
		CategoryTotalEntity newEntity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == newCategoryId
		);

		await Assert.That(value: oldEntity.Total).IsEqualTo(expected: 0m);
		await Assert.That(value: newEntity.Total).IsEqualTo(expected: 9000m);
	}
}