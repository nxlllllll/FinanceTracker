namespace FinanceTracker.Infrastructure.Database.Context.Role;

public sealed class RolePermissionEntity
{
	public Guid RoleId { get; init; }
	public string Permission { get; init; } = String.Empty;
}
