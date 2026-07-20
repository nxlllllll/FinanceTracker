namespace FinanceTracker.Infrastructure.Database.Context.Role;

public sealed class RoleEntity
{
	public Guid Id { get; init; }
	public string? SystemKey { get; init; }
	public string DisplayName { get; init; } = String.Empty;
	public DateTimeOffset CreatedAt { get; init; }
}
