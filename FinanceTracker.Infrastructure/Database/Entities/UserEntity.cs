namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class UserEntity 
{
	public Guid Id { get; init; }
	public string Email { get; set; } = String.Empty;
	public string PasswordHash { get; set; } = String.Empty;
	public string BaseCurrencyCode { get; set; } = String.Empty;
	public DateTime CreatedAt { get; init; }
}