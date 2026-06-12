using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Context.User;

public sealed class UserEntity
{
	public Guid Id { get; init; }
	public Email Email { get; set; }
	public string PasswordHash { get; set; } = String.Empty;
	public Core.ValueObjects.Currency BaseCurrencyCode { get; set; }
	public int RowVersion { get; set; }
	public DateTimeOffset CreatedAt { get; init; }
}