using FinanceTracker.Core.Services.Rebuild;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Cli.Commands;

/// <summary>
/// Replays the event store back into the account read model, for a single account or for all of them.
/// </summary>
public sealed class RebuildProjectionCommand(
	IAccountProjectionRebuilder rebuilder,
	ILogger<RebuildProjectionCommand> logger)
{
	public async Task<int> ExecuteForAccountAsync(
		string accountId,
		CancellationToken ct = default)
	{
		if (!Guid.TryParse(input: accountId, result: out Guid parsed))
		{
			logger.ZLogError(message: $"'{accountId}' is not a valid account id.");
			return 1;
		}

		logger.ZLogInformation(message: $"Rebuilding the projection for account {parsed}.");

		await rebuilder.RebuildAsync(accountId: parsed, ct: ct);

		logger.ZLogInformation(message: $"Done. An account with no events and no snapshot is left untouched — check the log above to tell that apart from a successful rebuild.");
		return 0;
	}

	/// <summary>
	/// Rebuilds every account.
	/// </summary>
	public async Task<int> ExecuteForAllAsync(
		bool confirmed,
		int batchSize,
		CancellationToken ct = default)
	{
		if (!confirmed)
		{
			logger.ZLogError(message: $"Refusing to rebuild every account without --yes. This overwrites the whole account read model, and a mistyped argument should not be able to start it.");
			return 1;
		}

		if (batchSize <= 0)
		{
			logger.ZLogError(message: $"--batch-size must be greater than zero, got {batchSize}.");
			return 1;
		}

		logger.ZLogInformation(message: $"""
			Rebuilding the projection for every account, {batchSize} at a time.
			Reads are served from the projection while this runs, so an account is briefly missing between
			its row being deleted and its events being replayed. Prefer a quiet window.
		""");

		await rebuilder.RebuildAllAsync(batchSize: batchSize, ct: ct);

		logger.ZLogInformation(message: $"Done. Accounts that failed individually were logged and skipped — the counts are in the line above.");
		return 0;
	}
}
