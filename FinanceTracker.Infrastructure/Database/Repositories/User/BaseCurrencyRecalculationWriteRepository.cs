using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

/// <summary>Drives the category-total rebuild state machine.</summary>
public sealed class BaseCurrencyRecalculationWriteRepository(
	FinanceTrackerContext context
) : IBaseCurrencyRecalculationWriteRepository
{
	public Task RequestAsync(
		Guid userId,
		Core.ValueObjects.Currency targetCurrency,
		DateTimeOffset requestedAt,
		CancellationToken ct = default
	) => context.RequestBaseCurrencyRecalculationAsync(
		userId: userId,
		targetCurrency: targetCurrency.Value,
		requestedAt: requestedAt,
		ct: ct
	);

	public async Task<IReadOnlyList<BaseCurrencyRecalculation>> ClaimPendingBatchAsync(
		int batchSize,
		TimeSpan leaseDuration,
		DateTimeOffset now,
		CancellationToken ct = default)
	{
		List<BaseCurrencyRecalculationClaimDto> claimed = await context.ClaimBaseCurrencyRecalculationsAsync(
			batchSize: batchSize,
			now: now,
			leaseUntil: now.Add(timeSpan: leaseDuration),
			ct: ct
		);

		return claimed.Select(selector: row => new BaseCurrencyRecalculation(
			UserId: row.UserId,
			Status: BaseCurrencyRecalculationStatus.InProgress,
			TargetCurrency: Core.ValueObjects.Currency.Reconstitute(value: row.TargetCurrency),
			RequestedAt: row.RequestedAt,
			Attempts: row.Attempts,
			LastError: row.LastError
		)).ToList();
	}

	/// <summary>
	/// Completing only when <paramref name="targetCurrency"/> still matches is the guard against a
	/// second change landing mid-rebuild: the row would already aim elsewhere, and these results
	/// describe a currency nobody asked for any more. Zero rows affected says exactly that.
	/// </summary>
	public async Task<bool> CompleteAsync(
		Guid userId,
		Core.ValueObjects.Currency targetCurrency,
		CancellationToken ct = default)
	{
		int affected = await context.BaseCurrencyRecalculations
			.Where(predicate: r => r.UserId == userId && r.TargetCurrency == targetCurrency.Value)
			.ExecuteUpdateAsync(setPropertyCalls: setters => setters
				.SetProperty(propertyExpression: r => r.Status, valueExpression: _ => BaseCurrencyRecalculationStatus.Completed)
				.SetProperty(propertyExpression: r => r.LockedUntil, valueExpression: _ => null)
				.SetProperty(propertyExpression: r => r.LastError, valueExpression: _ => null),
				cancellationToken: ct
			);

		return affected > 0;
	}

	/// <summary>
	/// Releasing the lease is what allows a retry; reaching <paramref name="maxAttempts"/> is what
	/// stops one. A rebuild that keeps failing is not helped by running it again, and retrying
	/// forever would keep it out of sight instead of leaving it visible as failed.
	/// </summary>
	public Task FailAttemptAsync(
		Guid userId,
		string error,
		int maxAttempts,
		CancellationToken ct = default)
	{
		return context.BaseCurrencyRecalculations
			.Where(predicate: r => r.UserId == userId)
			.ExecuteUpdateAsync(setPropertyCalls: setters => setters
				.SetProperty(propertyExpression: r => r.Attempts, valueExpression: r => r.Attempts + 1)
				.SetProperty(propertyExpression: r => r.LastError, valueExpression: _ => error)
				.SetProperty(propertyExpression: r => r.LockedUntil, valueExpression: _ => null)
				.SetProperty(propertyExpression: r => r.Status, valueExpression: r =>
					r.Attempts + 1 >= maxAttempts ? BaseCurrencyRecalculationStatus.Failed : BaseCurrencyRecalculationStatus.Pending
				),
				cancellationToken: ct
			);
	}
}
