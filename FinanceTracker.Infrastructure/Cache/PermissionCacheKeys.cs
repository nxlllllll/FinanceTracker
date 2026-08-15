using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Cache;

/// <summary>
/// The single owner of every Redis key holding a user's authorization state.
/// </summary>
public static class PermissionCacheKeys
{
	public static string Permissions(Guid userId)
		=> $"permissions:{userId}";

	public static string SystemRoleKey(Guid userId, SystemRole systemKey)
		=> $"roles:{userId}:{systemKey}";

	public static IReadOnlyList<string> AllForUser(Guid userId)
	{
		SystemRole[] systemRoles = Enum.GetValues<SystemRole>();

		List<string> keys = new List<string>(capacity: systemRoles.Length + 1)
		{
			Permissions(userId: userId)
		};

		keys.AddRange(collection: systemRoles.Select(selector: systemKey => SystemRoleKey(userId: userId, systemKey: systemKey)));

		return keys;
	}
}
