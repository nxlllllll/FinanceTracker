namespace FinanceTracker.Infrastructure.Database.Context.Account;

public sealed class AccountBalanceEntity
{
	public Guid AccountId { get; init; }
	public decimal Balance { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
