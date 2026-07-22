namespace FinanceTracker.Infrastructure.Database.Context.Role;

public sealed class UserRoleEntity
{
	public Guid UserId { get; init; }
	public Guid RoleId { get; init; }
	public DateTimeOffset AssignedAt { get; init; }
}
