namespace FinanceTracker.Infrastructure.Database.Context.Account;

public sealed class AccountBalanceAppliedEventEntity
{
	public Guid AccountId { get; init; }
	public int Version { get; init; }
	public DateTimeOffset AppliedAt { get; init; }
}
