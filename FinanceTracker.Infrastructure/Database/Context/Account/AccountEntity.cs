using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Context.Account;

public sealed class AccountEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Name Name { get; init; }
	public AccountType AccountType { get; init; }
	public Core.ValueObjects.Currency Currency { get; init; }
	public bool IsArchived { get; init; }
	public int LastVersion { get; init; }
	public DateTimeOffset CreatedAt { get; init; }
}
