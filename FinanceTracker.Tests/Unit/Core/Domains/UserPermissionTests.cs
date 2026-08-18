using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.UserPermission;
using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class UserPermissionTests
{
	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;

	[Test]
	public async Task Create_WithValidData_ShouldRaiseUserPermissionCreatedEvent()
	{
		UserPermission userPermission = UserPermissionFactory.Create();

		await Assert.That(value: userPermission.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: userPermission.Events[0]).IsTypeOf<UserPermissionCreated>();
	}

	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.CreateVersion7();

		UserPermission userPermission = UserPermissionFactory.Create(userId: userId);

		await Assert.That(value: userPermission.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: userPermission.Permissions).IsEmpty();
		await Assert.That(value: userPermission.Version).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Grant_WithNewPermission_ShouldRaisePermissionGrantedEvent()
	{
		UserPermission userPermission = UserPermissionFactory.Create();
		userPermission.ClearEvents();
		Permission permission = Permission.Create(resource: Resource.Account, action: PermissionAction.Write).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = userPermission.Grant(
			occurredAt: Now,
			grantedBy: Guid.CreateVersion7(),
			permission: permission
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: userPermission.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: userPermission.Events[0]).IsTypeOf<PermissionGranted>();
	}

	[Test]
	public async Task Grant_WithNewPermission_ShouldAddToPermissions()
	{
		UserPermission userPermission = UserPermissionFactory.Create();
		Permission permission = Permission.Create(resource: Resource.Balance, action: PermissionAction.Read).Value!;

		_ = userPermission.Grant(occurredAt: Now, grantedBy: Guid.CreateVersion7(), permission: permission);

		await Assert.That(value: userPermission.Permissions).Contains(expected: "balance:read");
	}

	[Test]
	public async Task Grant_AlreadyGrantedPermission_ShouldBeIdempotentAndRaiseNoEvent()
	{
		UserPermission userPermission = UserPermissionFactory.CreateWithGrant(resource: Resource.Account, action: PermissionAction.Write);
		Permission permission = Permission.Create(resource: Resource.Account, action: PermissionAction.Write).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = userPermission.Grant(
			occurredAt: Now,
			grantedBy: Guid.CreateVersion7(),
			permission: permission
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: userPermission.Events).IsEmpty();
		await Assert.That(value: userPermission.Permissions).Count().IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Revoke_HeldPermission_ShouldRaisePermissionRevokedEvent()
	{
		UserPermission userPermission = UserPermissionFactory.CreateWithGrant(resource: Resource.Transaction, action: PermissionAction.Write);
		Permission permission = Permission.Create(resource: Resource.Transaction, action: PermissionAction.Write).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = userPermission.Revoke(
			occurredAt: Now,
			revokedBy: Guid.CreateVersion7(),
			permission: permission
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: userPermission.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: userPermission.Events[0]).IsTypeOf<PermissionRevoked>();
		await Assert.That(value: userPermission.Permissions).IsEmpty();
	}

	[Test]
	public async Task Revoke_NotHeldPermission_ShouldBeIdempotentAndRaiseNoEvent()
	{
		UserPermission userPermission = UserPermissionFactory.Create();
		userPermission.ClearEvents();
		Permission permission = Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = userPermission.Revoke(
			occurredAt: Now,
			revokedBy: Guid.CreateVersion7(),
			permission: permission
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: userPermission.Events).IsEmpty();
	}

	[Test]
	public async Task ReconstituteFromHistory_ShouldRebuildPermissionsSet()
	{
		UserPermission original = UserPermissionFactory.CreateWithGrant(resource: Resource.Budget, action: PermissionAction.Write);
		IReadOnlyList<IEvent> history =
		[
			new UserPermissionCreated(
				Id: Guid.CreateVersion7(),
				UserId: original.UserId,
				Version: 1,
				OccurredAt: Now
			),
			new PermissionGranted(
				Id: Guid.CreateVersion7(),
				UserId: original.UserId,
				GrantedBy: Guid.CreateVersion7(),
				Permission: "budget:write",
				Version: 2,
				OccurredAt: Now
			)
		];

		UserPermission reconstituted = UserPermission.ReconstituteFromHistory(history: history);

		await Assert.That(value: reconstituted.Permissions).Contains(expected: "budget:write");
		await Assert.That(value: reconstituted.Version).IsEqualTo(expected: 2);
	}
}
