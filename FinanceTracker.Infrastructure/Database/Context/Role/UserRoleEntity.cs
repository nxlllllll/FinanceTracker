namespace FinanceTracker.Infrastructure.Database.Context.Role;

public sealed class UserRoleEntity
{
	public Guid UserId { get; init; }
	public Guid RoleId { get; init; }
	public DateTimeOffset AssignedAt { get; init; }
	public Guid? AssignedBy { get; init; }
	public int LastVersion { get; init; }
	public bool IsActive { get; init; } = true;
	public DateTimeOffset? RemovedAt { get; init; }
	public Guid? RemovedBy { get; init; }
}
