using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

/// <summary>
/// A single grantable permission — a validated (<see cref="Resource"/>, <see cref="PermissionAction"/>) pair.
/// <see cref="Catalog"/> is the single source of truth for which actions are meaningful per resource;
/// it drives both runtime validation here and the read-only <c>permission_catalog</c> table
/// synced from it at startup (see the Infrastructure sync job).
/// </summary>
public sealed record Permission
{
	/// <summary>Which actions are valid for each resource. Update this alongside any change to <see cref="Resource"/> or <see cref="PermissionAction"/>.</summary>
	public static readonly IReadOnlyDictionary<Resource, IReadOnlySet<PermissionAction>> Catalog = new Dictionary<Resource, IReadOnlySet<PermissionAction>>
	{
		[Resource.Account] = new HashSet<PermissionAction> { PermissionAction.Read, PermissionAction.Write },
		[Resource.Balance] = new HashSet<PermissionAction> { PermissionAction.Read, PermissionAction.Write },
		[Resource.Transaction] = new HashSet<PermissionAction> { PermissionAction.Read, PermissionAction.Write, PermissionAction.Delete },
		[Resource.Budget] = new HashSet<PermissionAction> { PermissionAction.Read, PermissionAction.Write, PermissionAction.Delete },
		[Resource.Category] = new HashSet<PermissionAction> { PermissionAction.Read, PermissionAction.Write, PermissionAction.Delete },
		[Resource.RecurringTransaction] = new HashSet<PermissionAction> { PermissionAction.Read, PermissionAction.Write, PermissionAction.Delete },
		[Resource.Permission] = new HashSet<PermissionAction> { PermissionAction.Manage }
	};

	public Resource Resource { get; }
	public PermissionAction Action { get; }

	private Permission(Resource resource, PermissionAction action)
	{
		Resource = resource;
		Action = action;
	}

	/// <summary>Validates the (resource, action) pair against <see cref="Catalog"/>.</summary>
	public static Result<Permission, DomainException> Create(Resource resource, PermissionAction action)
	{
		if (!Catalog.TryGetValue(key: resource, value: out IReadOnlySet<PermissionAction>? allowedActions) || !allowedActions.Contains(item: action))
			return Result<Permission, DomainException>.Failure(error: new UnknownPermissionException(message: $"Action '{action}' is not valid for resource '{resource}'."));

		return Result<Permission, DomainException>.Success(value: new Permission(resource: resource, action: action));
	}

	/// <summary>Parses the persisted "resource:action" form (e.g. "account:write"), validating against <see cref="Catalog"/>.</summary>
	public static Result<Permission, DomainException> Create(string value)
	{
		string[] parts = value.Split(separator: ':', count: 2);
		if (parts.Length != 2 ||
			!Enum.TryParse(value: parts[0], ignoreCase: true, result: out Resource resource) ||
			!Enum.TryParse(value: parts[1], ignoreCase: true, result: out PermissionAction action)
		) return Result<Permission, DomainException>.Failure(error: new UnknownPermissionException(message: $"'{value}' is not a recognized permission."));

		return Create(resource: resource, action: action);
	}

	/// <summary>Reconstitutes without catalog validation — the value was already valid when the event was written.</summary>
	public static Permission Reconstitute(Resource resource, PermissionAction action)
		=> new Permission(resource: resource, action: action);

	public override string ToString() => $"{Resource}:{Action}".ToLowerInvariant();
}
