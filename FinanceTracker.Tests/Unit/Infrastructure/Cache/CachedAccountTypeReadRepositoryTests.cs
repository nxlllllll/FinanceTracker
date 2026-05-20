using System.Text.Json;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.AccountType;
using FinanceTracker.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedAccountTypeReadRepositoryTests
{
	private IAccountTypeReadRepository _inner = null!;
	private IDistributedCache _distributedCache = null!;
	private CachedAccountTypeReadRepository _repository = null!;

	private static readonly AccountTypeDto SavingsDto = new AccountTypeDto(
		Type: "savings",
		Name: "Сберегательный",
		Description: null
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<IAccountTypeReadRepository>();
		_distributedCache = Substitute.For<IDistributedCache>();

		RedisCache redisCache = new RedisCache(cache: _distributedCache);
		_repository = new CachedAccountTypeReadRepository(inner: _inner, redisCache: redisCache);

		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: (byte[]?)null);
	}

	[Test]
	public async Task GetAllAsync_OnCacheMiss_CallsInner()
	{
		_inner.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [SavingsDto]);

		await _repository.GetAllAsync();

		await _inner.Received(requiredNumberOfCalls: 1).GetAllAsync(ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetAllAsync_OnCacheHit_DoesNotCallInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: new List<AccountTypeDto> { SavingsDto }));

		await _repository.GetAllAsync();

		await _inner.DidNotReceive().GetAllAsync(ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetAllAsync_OnCacheHit_ReturnsCorrectValue()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: new List<AccountTypeDto> { SavingsDto }));

		IReadOnlyList<AccountTypeDto> result = await _repository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result[0].Type).IsEqualTo(expected: "savings");
	}

	[Test]
	public async Task GetByTypeAsync_OnCacheMiss_CallsInner()
	{
		_inner.GetByTypeAsync(type: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: SavingsDto);

		await _repository.GetByTypeAsync(type: "savings");

		await _inner.Received(requiredNumberOfCalls: 1).GetByTypeAsync(
			type: "savings",
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetByTypeAsync_OnCacheHit_DoesNotCallInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: SavingsDto));

		await _repository.GetByTypeAsync(type: "savings");

		await _inner.DidNotReceive().GetByTypeAsync(type: Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetByTypeAsync_WhenInnerReturnsNull_CachesNull()
	{
		_inner.GetByTypeAsync(type: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: (AccountTypeDto?)null);

		AccountTypeDto? result = await _repository.GetByTypeAsync(type: "unknown");

		await Assert.That(value: result).IsNull();
		await _distributedCache.Received(requiredNumberOfCalls: 1).SetAsync(
			key: Arg.Any<string>(),
			value: Arg.Any<byte[]>(),
			options: Arg.Any<DistributedCacheEntryOptions>(),
			token: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetByTypeAsync_WhenNullIsCached_ReturnsNullWithoutCallingInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: (AccountTypeDto?)null));

		AccountTypeDto? result = await _repository.GetByTypeAsync(type: "unknown");

		await Assert.That(value: result).IsNull();
		await _inner.DidNotReceive().GetByTypeAsync(type: Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
	}
}