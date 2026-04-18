namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class AccountEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public string Name { get; set; } = string.Empty;
	public string AccountType { get; init; } = String.Empty;
	public string Currency { get; init; } = String.Empty;
	public bool IsArchived { get; set; }
	public DateTime CreatedAt { get; init; }
}