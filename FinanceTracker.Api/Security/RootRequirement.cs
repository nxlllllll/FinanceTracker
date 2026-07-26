using Microsoft.AspNetCore.Authorization;

namespace FinanceTracker.Api.Security;

/// <summary>
/// Requires the current user to be root — a narrower, permission-independent check than
/// <see cref="PermissionRequirement"/>. Used for role administration (creating roles, editing
/// their permission sets, assigning/removing roles), which is deliberately out of reach even for
/// an admin who holds permission:manage.
/// </summary>
public sealed class RootRequirement : IAuthorizationRequirement;

