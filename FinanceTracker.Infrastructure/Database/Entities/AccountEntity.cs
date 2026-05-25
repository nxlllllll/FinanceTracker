using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class AccountEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Name Name { get; set; }
	public AccountType AccountType { get; init; }
	public Currency Currency { get; init; }
	public bool IsArchived { get; set; }
	public DateTimeOffset CreatedAt { get; init; }
}
