using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Role;
using FinanceTracker.Infrastructure.Database.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Database.Repositories.UserRole;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.UserPermission;

public sealed class UserPermissionReadRepositoryTests : DatabaseFixture
{
	private UserPermissionReadRepository _readRepository = null!;
	private UserPermissionWriteRepository _writeRepository = null!;
	private UserRoleWriteRepository _roleWriteRepository = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = new UserPermissionReadRepository(context: Context);
		_writeRepository = new UserPermissionWriteRepository(context: Context);
		_roleWriteRepository = new UserRoleWriteRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private Task GrantAsync(Guid userId, string permission) => _writeRepository.GrantAsync(
		@event: new PermissionGranted(
			Id: Guid.CreateVersion7(),
			UserId: userId,
			GrantedBy: Guid.CreateVersion7(),
			Permission: permission,
			Version: 1,
			OccurredAt: FakeDateProvider.Default.UtcNow
		),
		ct: CancellationToken.None
	);

	private async Task<Guid> SeededRoleWithPermissionsAsync(SystemRole systemKey, params string[] permissions)
	{
		Guid roleId = await Context.Roles.AsNoTracking()
			.Where(predicate: r => r.SystemKey == systemKey)
			.Select(selector: r => r.Id)
			.FirstAsync();

		await Context.RolePermissions.Where(predicate: rp => rp.RoleId == roleId).ExecuteDeleteAsync();

		foreach (string permission in permissions)
		{
			await Context.RolePermissions.AddAsync(entity: new RolePermissionEntity
			{
				RoleId = roleId,
				Permission = permission
			});
		}

		await Context.SaveChangesAsync();
		return roleId;
	}

	private Task AssignRoleAsync(
		Guid userId,
		Guid roleId,
		int version = 2
	) => _roleWriteRepository.AssignAsync(
		@event: new RoleAssigned(
			Id: Guid.CreateVersion7(),
			UserId: userId,
			RoleId: roleId,
			AssignedBy: Guid.CreateVersion7(),
			Version: version,
			OccurredAt: FakeDateProvider.Default.UtcNow
		),
		ct: CancellationToken.None
	);

	private Task RemoveRoleAsync(
		Guid userId,
		Guid roleId,
		int version = 3
	) => _roleWriteRepository.RemoveAsync(
		@event: new RoleRemoved(
			Id: Guid.CreateVersion7(),
			UserId: userId,
			RoleId: roleId,
			RemovedBy: Guid.CreateVersion7(),
			Version: version,
			OccurredAt: FakeDateProvider.Default.UtcNow
		),
		ct: CancellationToken.None
	);

	[Test]
	public async Task GetPermissionsAsync_ShouldIncludePermissionsCarriedByRoles()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleWithPermissionsAsync(systemKey: SystemRole.User, "category:read", "budget:read");
		await AssignRoleAsync(userId: userId, roleId: roleId);
		await GrantAsync(userId: userId, permission: "account:write");

		IReadOnlySet<string> result = await _readRepository.GetPermissionsAsync(userId: userId, ct: CancellationToken.None);

		await Assert.That(value: result).Contains(expected: "account:write");
		await Assert.That(value: result).Contains(expected: "category:read");
		await Assert.That(value: result).Contains(expected: "budget:read");
	}

	[Test]
	public async Task GetPermissionsAsync_WithAPermissionFromBothSources_ShouldReturnItOnce()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleWithPermissionsAsync(systemKey: SystemRole.User, "account:read");
		await AssignRoleAsync(userId: userId, roleId: roleId);
		await GrantAsync(userId: userId, permission: "account:read");

		IReadOnlySet<string> result = await _readRepository.GetPermissionsAsync(userId: userId, ct: CancellationToken.None);

		await Assert.That(value: result).Count().IsEqualTo(expected: 1);
	}

	[Test]
	public async Task GetPermissionsAsync_AfterRemovingARole_ShouldKeepTheDirectGrantOfTheSamePermission()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleWithPermissionsAsync(systemKey: SystemRole.User, "account:read");
		await AssignRoleAsync(userId: userId, roleId: roleId);
		await GrantAsync(userId: userId, permission: "account:read");

		await RemoveRoleAsync(userId: userId, roleId: roleId);

		IReadOnlySet<string> result = await _readRepository.GetPermissionsAsync(userId: userId, ct: CancellationToken.None);

		await Assert.That(value: result).Contains(expected: "account:read").Because(message: """
			This is the whole reason the two sources stay apart. Under the old flattened model, removing
			the role revoked the permission outright and took the personal grant with it — successfully,
			and without a trace.
		""");
	}

	[Test]
	public async Task GetPermissionsAsync_AfterRemovingARole_ShouldDropPermissionsOnlyThatRoleCarried()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleWithPermissionsAsync(systemKey: SystemRole.User, "budget:read");
		await AssignRoleAsync(userId: userId, roleId: roleId);

		await RemoveRoleAsync(userId: userId, roleId: roleId);

		IReadOnlySet<string> result = await _readRepository.GetPermissionsAsync(userId: userId, ct: CancellationToken.None);

		await Assert.That(value: result).DoesNotContain(expected: "budget:read");
	}

	[Test]
	public async Task GetPermissionsAsync_ForUnknownUser_ShouldReturnEmptySet()
	{
		IReadOnlySet<string> result = await _readRepository.GetPermissionsAsync(userId: Guid.CreateVersion7(), ct: CancellationToken.None);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetPermissionsAsync_AfterGrants_ShouldReturnAllOfThem()
	{
		Guid userId = Guid.CreateVersion7();
		await GrantAsync(userId: userId, permission: "account:read");
		await GrantAsync(userId: userId, permission: "balance:write");

		IReadOnlySet<string> result = await _readRepository.GetPermissionsAsync(userId: userId, ct: CancellationToken.None);

		await Assert.That(value: result).Count().IsEqualTo(expected: 2);
		await Assert.That(value: result).Contains(expected: "account:read");
		await Assert.That(value: result).Contains(expected: "balance:write");
	}

	[Test]
	public async Task GetPermissionsAsync_ShouldNotReturnOtherUsersPermissions()
	{
		Guid userA = Guid.CreateVersion7();
		Guid userB = Guid.CreateVersion7();
		await GrantAsync(userId: userA, permission: "account:write");
		await GrantAsync(userId: userB, permission: "budget:delete");

		IReadOnlySet<string> resultA = await _readRepository.GetPermissionsAsync(userId: userA, ct: CancellationToken.None);

		await Assert.That(value: resultA).Count().IsEqualTo(expected: 1);
		await Assert.That(value: resultA).Contains(expected: "account:write");
	}
}
