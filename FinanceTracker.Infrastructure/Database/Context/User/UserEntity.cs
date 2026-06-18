using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Context.User;

public sealed class UserEntity
{
	public Guid Id { get; init; }
	public Email Email { get; init; }
	public string PasswordHash { get; init; } = String.Empty;
	public Core.ValueObjects.Currency BaseCurrencyCode { get; init; }
	public int RowVersion { get; init; }
	public DateTimeOffset CreatedAt { get; init; }
}