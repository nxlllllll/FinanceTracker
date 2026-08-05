using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.Role;
using FinanceTracker.Infrastructure.Database.Repositories.UserRole;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Role;

public sealed class RoleRepositoryTests : DatabaseFixture
{
	private RoleRepository _repository = null!;
	private UserBuilder _userBuilder = null!;
	private UserRoleWriteRepository _membershipWriter = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_repository = new RoleRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
		_membershipWriter = new UserRoleWriteRepository(context: Context);
	}

	private static IReadOnlySet<Permission> Perms(params (Resource, PermissionAction)[] pairs)
		=> pairs.Select(selector: p => Permission.Create(resource: p.Item1, action: p.Item2).Value!).ToHashSet();

	private Task<Guid> CreateRoleAsync(string displayName, IReadOnlySet<Permission> permissions)
	{
		return UnitOfWork.ExecuteInTransactionAsync(operation: async () => await _repository.CreateAsync(
			displayName: Name.Create(value: displayName).Value!,
			permissions: permissions,
			createdAt: FakeDateProvider.Default.UtcNow,
			ct: CancellationToken.None
		), ct: CancellationToken.None);
	}

	private Task AssignMembershipAsync(
		Guid userId,
		Guid roleId
	) => _membershipWriter.AssignAsync(
		@event: new RoleAssigned(
			Id: Guid.CreateVersion7(),
			UserId: userId,
			RoleId: roleId,
			AssignedBy: Guid.CreateVersion7(),
			Version: 2,
			OccurredAt: FakeDateProvider.Default.UtcNow
		),
		ct: CancellationToken.None
	);

	[Test]
	public async Task CreateAsync_ThenGetByIdAsync_ShouldReturnRoleWithPermissions()
	{
		Guid roleId = await CreateRoleAsync(
			displayName: "Accountant",
			permissions: Perms((Resource.Account, PermissionAction.Read), (Resource.Budget, PermissionAction.Write))
		);

		RoleDto? role = await _repository.GetByIdAsync(roleId: roleId, ct: CancellationToken.None);

		await Assert.That(value: role).IsNotNull();
		await Assert.That(value: role!.DisplayName.Value).IsEqualTo(expected: "Accountant");
		await Assert.That(value: role.SystemKey).IsNull();
		await Assert.That(value: role.Permissions).Count().IsEqualTo(expected: 2);
	}

	[Test]
	public async Task CreateAsync_WithoutACommit_ShouldNotPersistAnything()
	{
		Guid roleId = await _repository.CreateAsync(
			displayName: Name.Create(value: "Uncommitted").Value!,
			permissions: Perms((Resource.Account, PermissionAction.Read)),
			createdAt: FakeDateProvider.Default.UtcNow,
			ct: CancellationToken.None
		);

		Context.ChangeTracker.Clear();

		RoleDto? role = await _repository.GetByIdAsync(roleId: roleId, ct: CancellationToken.None);

		await Assert.That(value: role).IsNull().Because(message: """
			The id is generated client-side, so it comes back looking perfectly valid whether or not the
			role was written. Persisting is the caller's job via the unit of work — if this ever returns
			a role, the repository has started committing on its own again.
		""");
	}

	[Test]
	public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
	{
		RoleDto? role = await _repository.GetByIdAsync(roleId: Guid.CreateVersion7(), ct: CancellationToken.None);

		await Assert.That(value: role).IsNull();
	}

	[Test]
	public async Task GetBySystemKeyAsync_ForSeededRole_ShouldReturnIt()
	{
		RoleDto? role = await _repository.GetBySystemKeyAsync(systemKey: SystemRole.User, ct: CancellationToken.None);

		await Assert.That(value: role).IsNotNull();
		await Assert.That(value: role!.SystemKey).IsEqualTo(expected: SystemRole.User);
	}

	[Test]
	public async Task ReplacePermissionsAsync_ShouldOverwriteEntirePermissionSet()
	{
		Guid roleId = await CreateRoleAsync(
			displayName: "Support",
			permissions: Perms((Resource.Account, PermissionAction.Read))
		);

		await UnitOfWork.ExecuteInTransactionAsync(operation: async () => await _repository.ReplacePermissionsAsync(
			roleId: roleId,
			permissions: Perms((Resource.Category, PermissionAction.Delete)),
			ct: CancellationToken.None
		), ct: CancellationToken.None);

		RoleDto? role = await _repository.GetByIdAsync(roleId: roleId, ct: CancellationToken.None);

		await Assert.That(value: role!.Permissions).Count().IsEqualTo(expected: 1);
		await Assert.That(value: role.Permissions).Contains(expected: Permission.Create(resource: Resource.Category, action: PermissionAction.Delete).Value!);
	}

	[Test]
	public async Task ReplacePermissionsAsync_WhenTheTransactionFails_ShouldLeaveTheOriginalSetIntact()
	{
		Guid roleId = await CreateRoleAsync(
			displayName: "Support",
			permissions: Perms((Resource.Account, PermissionAction.Read))
		);

		await Assert.That(async () => await UnitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await _repository.ReplacePermissionsAsync(
				roleId: roleId,
				permissions: Perms((Resource.Category, PermissionAction.Delete)),
				ct: CancellationToken.None
			);

			throw new InvalidOperationException(message: "Something fails after the delete but before the inserts land.");
		}, ct: CancellationToken.None)).Throws<InvalidOperationException>();

		Context.ChangeTracker.Clear();

		RoleDto? role = await _repository.GetByIdAsync(roleId: roleId, ct: CancellationToken.None);

		await Assert.That(value: role!.Permissions).Contains(expected: Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!).Because(message: """
			The delete runs immediately while the inserts wait for the flush. Without a transaction
			around both, a failure in that gap strips the role's permissions for good — and nothing
			reports it, because no exception ever reaches the caller about the missing rows.
		""");
	}

	[Test]
	public async Task CountMembersWithSystemKeyAsync_ShouldCountOnlyMatchingSystemRole()
	{
		RoleDto? rootRole = await _repository.GetBySystemKeyAsync(systemKey: SystemRole.Root, ct: CancellationToken.None);
		Guid userA = await _userBuilder.CreateAsync();
		Guid userB = await _userBuilder.CreateAsync();

		await AssignMembershipAsync(userId: userA, roleId: rootRole!.Id);
		await AssignMembershipAsync(userId: userB, roleId: rootRole.Id);

		int count = await _repository.CountMembersWithSystemKeyAsync(systemKey: SystemRole.Root, ct: CancellationToken.None);

		await Assert.That(value: count).IsEqualTo(expected: 2);
	}
}
