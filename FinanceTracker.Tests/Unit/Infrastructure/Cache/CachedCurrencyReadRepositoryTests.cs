using System.Text.Json;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedCurrencyReadRepositoryTests
{
	private ICurrencyReadRepository _inner = null!;
	private IDistributedCache _distributedCache = null!;
	private CachedCurrencyReadRepository _repository = null!;

	private static readonly CurrencyInfo RubDto = new CurrencyInfo(
		Code: "RUB",
		Name: "���������� �����",
		Symbol: "?",
		IsActive: true
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<ICurrencyReadRepository>();
		_distributedCache = Substitute.For<IDistributedCache>();

		RedisCache redisCache = new RedisCache(cache: _distributedCache);
		_repository = new CachedCurrencyReadRepository(inner: _inner, redisCache: redisCache);

		_distributedCache
			.GetAsync(key: Arg.Any<string>(), token: Arg.Any<CancellationToken>())
			.Returns(returnThis: (byte[]?)null);
	}

	[Test]
	public async Task GetAllAsync_OnCacheMiss_CallsInner()
	{
		_inner.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [RubDto]);

		await _repository.GetAllAsync();

		await _inner.Received(requiredNumberOfCalls: 1).GetAllAsync(ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetAllAsync_OnCacheMiss_StoresResultInCache()
	{
		_inner.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [RubDto]);

		await _repository.GetAllAsync();

		await _distributedCache.Received(requiredNumberOfCalls: 1).SetAsync(
			key: Arg.Any<string>(),
			value: Arg.Any<byte[]>(),
			options: Arg.Any<DistributedCacheEntryOptions>(),
			token: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetAllAsync_OnCacheHit_DoesNotCallInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(),
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: new List<CurrencyInfo> { RubDto }));

		await _repository.GetAllAsync();

		await _inner.DidNotReceive().GetAllAsync(ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetAllAsync_OnCacheHit_ReturnsCorrectValue()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: new List<CurrencyInfo> { RubDto }));

		IReadOnlyList<CurrencyInfo> result = await _repository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result[0].Code).IsEqualTo(expected: "RUB");
	}

	[Test]
	public async Task GetByCodeAsync_OnCacheMiss_CallsInner()
	{
		_inner.GetByCodeAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: RubDto);

		await _repository.GetByCodeAsync(code: "RUB");

		await _inner.Received(requiredNumberOfCalls: 1).GetByCodeAsync(
			code: "RUB",
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetByCodeAsync_OnCacheHit_DoesNotCallInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(),
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: RubDto));

		await _repository.GetByCodeAsync(code: "RUB");

		await _inner.DidNotReceive().GetByCodeAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetByCodeAsync_WhenInnerReturnsNull_CachesNull()
	{
		_inner.GetByCodeAsync(
			code: Arg.Any<string>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (CurrencyInfo?)null);

		CurrencyInfo? result = await _repository.GetByCodeAsync(code: "XXX");

		await Assert.That(value: result).IsNull();
		await _distributedCache.Received(requiredNumberOfCalls: 1).SetAsync(
			key: Arg.Any<string>(),
			value: Arg.Any<byte[]>(),
			options: Arg.Any<DistributedCacheEntryOptions>(),
			token: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetByCodeAsync_WhenNullIsCached_ReturnsNullWithoutCallingInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: (CurrencyInfo?)null));

		CurrencyInfo? result = await _repository.GetByCodeAsync(code: "XXX");

		await Assert.That(value: result).IsNull();
		await _inner.DidNotReceive().GetByCodeAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task ExistsAsync_OnCacheMiss_CallsInner()
	{
		_inner.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		await _repository.ExistsAsync(code: "RUB");

		await _inner.Received(requiredNumberOfCalls: 1).ExistsAsync(
			code: "RUB",
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ExistsAsync_OnCacheHit_DoesNotCallInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: true));

		await _repository.ExistsAsync(code: "RUB");

		await _inner.DidNotReceive().ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task ExistsAsync_WhenFalseIsCached_ReturnsFalseWithoutCallingInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: false));

		bool result = await _repository.ExistsAsync(code: "XXX");

		await Assert.That(value: result).IsFalse();
		await _inner.DidNotReceive().ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
	}
}
