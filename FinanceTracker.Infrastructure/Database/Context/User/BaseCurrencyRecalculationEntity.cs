using FinanceTracker.Core.Domains.User;

namespace FinanceTracker.Infrastructure.Database.Context.User;

public sealed class BaseCurrencyRecalculationEntity
{
	public Guid UserId { get; init; }
	public BaseCurrencyRecalculationStatus Status { get; init; }
	public string TargetCurrency { get; init; } = String.Empty;
	public DateTimeOffset RequestedAt { get; init; }
	public DateTimeOffset? LockedUntil { get; init; }
	public int Attempts { get; init; }
	public string? LastError { get; init; }
}
