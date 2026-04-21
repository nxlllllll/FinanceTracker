namespace FinanceTracker.Infrastructure.Database.Entities;

public class AccountTypeEntity
{
	public string Type { get; init; } = String.Empty;
	public string Name { get; init; } = String.Empty;
	public string? Description { get; init; }
}