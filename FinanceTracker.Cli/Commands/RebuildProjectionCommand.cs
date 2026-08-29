using FinanceTracker.Core.Services.Rebuild;
using FinanceTracker.Infrastructure.Services.Rebuild;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Cli.Commands;

/// <summary>
/// Replays the event log back into a projection, for one aggregate or for all of them.
/// </summary>
public sealed class RebuildProjectionCommand(
	ProjectionRegistry registry,
	ProjectionRebuilder rebuilder,
	ILogger<RebuildProjectionCommand> logger)
{
	public async Task<int> ExecuteForAggregateAsync(
		string projectionName,
		string aggregateId,
		CancellationToken ct = default)
	{
		if (Resolve(projectionName: projectionName) is not (var projection, var aggregateType))
			return 1;

		if (!Guid.TryParse(input: aggregateId, result: out Guid parsed))
		{
			logger.ZLogError(message: $"'{aggregateId}' is not a valid id.");
			return 1;
		}

		await rebuilder.RebuildAsync(
			projection: projection,
			aggregateType: aggregateType,
			aggregateId: parsed,
			ct: ct
		);

		return 0;
	}

	public async Task<int> ExecuteForAllAsync(
		string projectionName,
		bool confirmed,
		int batchSize,
		int parallelism,
		CancellationToken ct = default)
	{
		if (Resolve(projectionName: projectionName) is null)
			return 1;

		if (!confirmed)
		{
			logger.ZLogError(message: $"Refusing to rebuild every '{projectionName}' without --yes. This overwrites the whole read model, and a mistyped argument should not be able to start it.");
			return 1;
		}

		if (batchSize <= 0 || parallelism <= 0)
		{
			logger.ZLogError(message: $"--batch-size and --parallelism must both be greater than zero.");
			return 1;
		}

		logger.ZLogInformation(message: $"""
			Rebuilding every '{projectionName}'.
			Reads are served from the projection while this runs, so an aggregate is briefly missing between
			its rows being erased and its events being replayed. Prefer a quiet window.
		""");

		await rebuilder.RebuildAllAsync(
			projectionName: projectionName,
			batchSize: batchSize,
			parallelism: parallelism,
			ct: ct
		);

		return 0;
	}

	private (IProjectionRebuild Projection, string AggregateType)? Resolve(string projectionName)
	{
		(IProjectionRebuild Projection, string AggregateType)? resolved = registry.Resolve(name: projectionName);

		if (resolved is null)
			logger.ZLogError(message: $"No projection named '{projectionName}'. Known: {String.Join(separator: ", ", values: ProjectionRegistry.Names)}.");

		return resolved;
	}
}
