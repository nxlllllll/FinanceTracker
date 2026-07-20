using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Role;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Role;

public sealed class RoleRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider
) : IRoleRepository
{
	public async Task<Guid> CreateAsync(
		Name displayName,
		IReadOnlySet<Permission> permissions,
		CancellationToken ct = default)
	{
		RoleEntity role = new RoleEntity
		{
			Id = Guid.CreateVersion7(),
			SystemKey = null,
			DisplayName = displayName.Value,
			CreatedAt = dateProvider.UtcNow
		};

		await context.Roles.AddAsync(entity: role, cancellationToken: ct);

		foreach (Permission permission in permissions)
		{
			await context.RolePermissions.AddAsync(
				entity: new RolePermissionEntity { RoleId = role.Id, Permission = permission.ToString() },
				cancellationToken: ct
			);
		}

		await context.SaveChangesAsync(cancellationToken: ct);
		return role.Id;
	}

	public async Task<RoleDto?> GetByIdAsync(
		Guid roleId,
		CancellationToken ct = default
	) => await LoadAsync(predicate: r => r.Id == roleId, ct: ct);

	public async Task<RoleDto?> GetBySystemKeyAsync(
		string systemKey,
		CancellationToken ct = default
	) => await LoadAsync(predicate: r => r.SystemKey == systemKey, ct: ct);

	private async Task<RoleDto?> LoadAsync(
		System.Linq.Expressions.Expression<Func<RoleEntity, bool>> predicate,
		CancellationToken ct)
	{
		RoleEntity? role = await context.Roles.AsNoTracking().FirstOrDefaultAsync(predicate: predicate, cancellationToken: ct);
		if (role is null)
			return null;

		List<string> rawPermissions = await context.RolePermissions.AsNoTracking()
			.Where(predicate: p => p.RoleId == role.Id)
			.Select(selector: p => p.Permission)
			.ToListAsync(cancellationToken: ct);

		IReadOnlySet<Permission> permissions = rawPermissions.Select(selector: raw => Permission.Create(value: raw).Value!).ToHashSet();

		return new RoleDto(
			Id: role.Id,
			SystemKey: role.SystemKey,
			DisplayName: Name.Reconstitute(value: role.DisplayName),
			Permissions: permissions
		);
	}

	public async Task ReplacePermissionsAsync(
		Guid roleId,
		IReadOnlySet<Permission> permissions,
		CancellationToken ct = default)
	{
		await context.RolePermissions.Where(predicate: p => p.RoleId == roleId).ExecuteDeleteAsync(cancellationToken: ct);

		foreach (Permission permission in permissions)
		{
			await context.RolePermissions.AddAsync(
				entity: new RolePermissionEntity { RoleId = roleId, Permission = permission.ToString() },
				cancellationToken: ct
			);
		}

		await context.SaveChangesAsync(cancellationToken: ct);
	}

	public async Task AssignToUserAsync(
		Guid userId,
		Guid roleId,
		DateTimeOffset assignedAt,
		CancellationToken ct = default)
	{
		bool exists = await context.UserRoles.AnyAsync(predicate: ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken: ct);
		if (exists)
			return;

		await context.UserRoles.AddAsync(
			entity: new UserRoleEntity
			{
				UserId = userId,
				RoleId = roleId,
				AssignedAt = assignedAt
			},
			cancellationToken: ct
		);
		await context.SaveChangesAsync(cancellationToken: ct);
	}

	public async Task RemoveFromUserAsync(
		Guid userId,
		Guid roleId,
		CancellationToken ct = default
	) => await context.UserRoles.Where(predicate: ur => ur.UserId == userId && ur.RoleId == roleId).ExecuteDeleteAsync(cancellationToken: ct);

	public async Task<IReadOnlyList<Guid>> GetMemberUserIdsAsync(
		Guid roleId,
		CancellationToken ct = default
	) => await context.UserRoles.AsNoTracking().Where(predicate: ur => ur.RoleId == roleId).Select(selector: ur => ur.UserId).ToListAsync(cancellationToken: ct);

	public async Task<int> CountMembersWithSystemKeyAsync(
		string systemKey,
		CancellationToken ct = default)
	{
		return await context.UserRoles.Join(
			inner: context.Roles,
			outerKeySelector: ur => ur.RoleId,
			innerKeySelector: r => r.Id,
			resultSelector: (ur, r) => r.SystemKey
		).CountAsync(predicate: key => key == systemKey, cancellationToken: ct);
	}
}
