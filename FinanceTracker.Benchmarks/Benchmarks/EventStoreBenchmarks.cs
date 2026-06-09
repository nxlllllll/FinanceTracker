using System.Data.Common;
using BenchmarkDotNet.Attributes;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.EventStore;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Benchmarks.Benchmarks;

public class EventStoreBenchmarks : BenchmarkBase
{
	private Guid _aggregateId;

	[GlobalSetup]
	public async Task GlobalSetup()
	{
		_aggregateId = Guid.NewGuid();

		await using FinanceTrackerContext context = Db.CreateContext();
		await using DbConnection connection = context.Database.GetDbConnection();
		await connection.OpenAsync();

		await using DbCommand cmd = connection.CreateCommand();
		cmd.CommandText = $"""
		    INSERT INTO events (id, aggregate_id, aggregate_type, event_type, version, payload, occurred_at, created_at)
		    SELECT
		        gen_random_uuid(),
		        '{_aggregateId}',
		        'Account',
		        'account.debited',
		        i,
		        @payload::jsonb,
		        now() - (i || ' seconds')::interval,
		        now()
		    FROM generate_series(1, 1000) i
		""";

		DbParameter payloadParam = cmd.CreateParameter();
		payloadParam.ParameterName = "@payload";
		payloadParam.Value = "{\"Amount\": 100}";
		cmd.Parameters.Add(payloadParam);

		cmd.CommandTimeout = 60;
		await cmd.ExecuteNonQueryAsync();
	}

	[IterationSetup]
	public override void IterationSetup()
		=> base.IterationSetup();
	
	[Benchmark]
	public async Task LoadEventsForAggregateAsync()
	{
		await Context.Set<EventEntity>().AsNoTracking()
			.Where(e => e.AggregateId == _aggregateId && e.AggregateType == AggregateTypeNames.Account)
			.OrderBy(e => e.Version)
			.ToListAsync();
	}

	[Benchmark]
	public async Task GetAggregateIdsAsync()
	{
		await Context.Set<EventEntity>().AsNoTracking()
			.Where(e => e.AggregateType == AggregateTypeNames.Account)
			.Select(e => e.AggregateId)
			.Distinct()
			.ToListAsync();
	}
}