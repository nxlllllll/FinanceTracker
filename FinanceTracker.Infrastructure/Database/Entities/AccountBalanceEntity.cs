namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class AccountBalanceEntity
{
	public Guid AccountId { get; init; }
	public decimal Balance { get; set; }
	public int LastVersion { get; set; }
	public DateTime UpdatedAt { get; set; }
}