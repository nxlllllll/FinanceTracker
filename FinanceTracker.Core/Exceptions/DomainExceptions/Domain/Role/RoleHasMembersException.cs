namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Role;

/// <summary>Raised when a role is deleted while users still belong to it.</summary>
[ErrorCode(code: "role.has_members")]
public sealed class RoleHasMembersException(
	Guid roleId,
	int memberCount
) : DomainException(message: $"The role still has {memberCount} member(s). Remove them before deleting it.")
{
	public Guid RoleId { get; init; } = roleId;
	public int MemberCount { get; init; } = memberCount;
}
