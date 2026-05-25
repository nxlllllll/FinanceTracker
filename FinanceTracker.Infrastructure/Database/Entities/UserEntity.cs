using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class UserEntity 
{
	public Guid Id { get; init; }
	public Email Email { get; set; }
	public string PasswordHash { get; set; } = String.Empty;
	public Currency BaseCurrencyCode { get; set; }
	public DateTimeOffset CreatedAt { get; init; }
}
