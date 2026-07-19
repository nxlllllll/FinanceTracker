namespace FinanceTracker.Infrastructure.Database.Context.UserPermission;

public sealed class UserPermissionEntity
{
	public Guid UserId { get; init; }
	public string Permission { get; init; } = String.Empty;
	public DateTimeOffset GrantedAt { get; init; }
}
