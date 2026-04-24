using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class AccountEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public string Name { get; set; } = string.Empty;
	public AccountType AccountType { get; init; }
	public string Currency { get; init; } = String.Empty;
	public bool IsArchived { get; set; }
	public DateTime CreatedAt { get; init; }
}