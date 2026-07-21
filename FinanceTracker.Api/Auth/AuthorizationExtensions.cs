using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Api.Auth;

public static class AuthorizationExtensions
{
	public const string RootPolicyName = "root";

	/// <summary>
	/// Requires the caller to hold the given permission (e.g. "account:write"). Builds the
	/// policy name that <see cref="PermissionPolicyProvider"/> parses dynamically — no manual
	/// policy registration needed for each permission.
	/// </summary>
	public static TBuilder RequirePermission<TBuilder>(
		this TBuilder builder,
		string permission
	) where TBuilder : IEndpointConventionBuilder
	{
		return builder.RequireAuthorization(policyNames: $"permission:{permission}");
	}

	/// <summary>
	/// Type-safe variant: validates the (resource, action) pair against <see cref="Permission.Catalog"/>
	/// at endpoint-mapping time (i.e. once, at startup), so a typo like <c>Resource.Balance</c> paired
	/// with an action <see cref="Permission.Catalog"/> doesn't allow for fails loudly during
	/// <c>MapEndpoints()</c> instead of silently misrouting requests at runtime.
	/// </summary>
	public static TBuilder RequirePermission<TBuilder>(
		this TBuilder builder,
		Resource resource,
		PermissionAction action
	) where TBuilder : IEndpointConventionBuilder
	{
		Result<Permission, DomainException> result = Permission.Create(resource: resource, action: action);

		if (result.IsFailure)
			throw new InvalidOperationException(message: $"'{resource}:{action}' is not a valid permission — check {nameof(Permission)}.{nameof(Permission.Catalog)}.");

		return builder.RequirePermission(permission: result.Value!.ToString());
	}

	public static TBuilder RequireRoot<TBuilder>(
		this TBuilder builder
	) where TBuilder : IEndpointConventionBuilder
	{
		return builder.RequireAuthorization(policyNames: RootPolicyName);
	}
}
