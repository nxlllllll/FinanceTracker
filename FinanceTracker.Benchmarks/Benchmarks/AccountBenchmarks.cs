using BenchmarkDotNet.Attributes;
using FinanceTracker.Infrastructure.Database.Repositories.Account;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class AccountBenchmarks : BenchmarkBase
{
	private AccountReadRepository _repository = null!;

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_repository = new AccountReadRepository(context: Context);
	}

	[Benchmark]
	public async Task GetByIdAsync()
		=> await _repository.GetByIdAsync(accountId: Db.AccountId, userId: Db.UserId);

	[Benchmark]
	public async Task GetAllAsync()
		=> await _repository.GetAllAsync(userId: Db.UserId);

	[Benchmark]
	public async Task GetAllAsync_ActiveOnly()
		=> await _repository.GetAllAsync(userId: Db.UserId, isArchived: false);

	[Benchmark]
	public async Task ExistAsync()
		=> await _repository.ExistAsync(accountId: Db.AccountId, userId: Db.UserId);
}