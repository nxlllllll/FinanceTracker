using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Infrastructure.Database.Context.UserPermission;
using FinanceTracker.Infrastructure.Database.Repositories.UserPermission;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.UserPermission;

public sealed class UserPermissionWriteRepositoryTests : DatabaseFixture
{
	private UserPermissionWriteRepository _repository = null!;

	[Before(hookType: Test)]
	public void Setup()
		=> _repository = new UserPermissionWriteRepository(context: Context);

	private static PermissionGranted BuildGrantedEvent(
		Guid userId,
		string permission,
		int version = 1
	) => new PermissionGranted(
		Id: Guid.CreateVersion7(),
		UserId: userId,
		GrantedBy: Guid.CreateVersion7(),
		Permission: permission,
		Version: version,
		OccurredAt: FakeDateProvider.Default.UtcNow
	);

	private static PermissionRevoked BuildRevokedEvent(
		Guid userId,
		string permission,
		int version = 2
	) => new PermissionRevoked(
		Id: Guid.CreateVersion7(),
		UserId: userId,
		RevokedBy: Guid.CreateVersion7(),
		Permission: permission,
		Version: version,
		OccurredAt: FakeDateProvider.Default.UtcNow
	);

	private Task<UserPermissionEntity?> FindAsync(
		Guid userId,
		string permission
	) => Context.UserPermissions.AsNoTracking().FirstOrDefaultAsync(predicate: e => e.UserId == userId && e.Permission == permission);

	private Task<int> CountActiveAsync(Guid userId)
		=> Context.UserPermissions.AsNoTracking().CountAsync(predicate: e => e.UserId == userId && e.IsActive);

	[Test]
	public async Task GrantAsync_ShouldInsertActiveRow()
	{
		Guid userId = Guid.CreateVersion7();

		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:write"), ct: CancellationToken.None);

		UserPermissionEntity? row = await FindAsync(userId: userId, permission: "account:write");

		await Assert.That(value: row).IsNotNull();
		await Assert.That(value: row!.IsActive).IsTrue();
		await Assert.That(value: row.LastVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task GrantAsync_CalledTwiceWithSameEvent_ShouldNotThrowAndShouldStayOneRow()
	{
		Guid userId = Guid.CreateVersion7();
		PermissionGranted @event = BuildGrantedEvent(userId: userId, permission: "balance:read");

		await Assert.That(action: async () =>
		{
			await _repository.GrantAsync(@event: @event, ct: CancellationToken.None);
			await _repository.GrantAsync(@event: @event, ct: CancellationToken.None);
		}).ThrowsNothing();

		int count = await Context.UserPermissions.CountAsync(predicate: e => e.UserId == userId);
		await Assert.That(value: count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task GrantAsync_ForDifferentPermissions_ShouldInsertBothRows()
	{
		Guid userId = Guid.CreateVersion7();

		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:read"), ct: CancellationToken.None);
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:write"), ct: CancellationToken.None);

		await Assert.That(value: await CountActiveAsync(userId: userId)).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task RevokeAsync_WithExistingRow_ShouldLeaveATombstoneInsteadOfDeleting()
	{
		Guid userId = Guid.CreateVersion7();
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "transaction:delete"), ct: CancellationToken.None);

		await _repository.RevokeAsync(@event: BuildRevokedEvent(userId: userId, permission: "transaction:delete"), ct: CancellationToken.None);

		UserPermissionEntity? row = await FindAsync(userId: userId, permission: "transaction:delete");

		await Assert.That(value: row).IsNotNull().Because(message: """
			The row has to survive: it is the record of which version revoked the permission, and without
			it a replayed grant would have nothing to compare against.
		""");
		await Assert.That(value: row!.IsActive).IsFalse();
		await Assert.That(value: row.LastVersion).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task RevokeAsync_WithNoExistingRow_ShouldNotThrow()
	{
		await Assert.That(action: async () => await _repository.RevokeAsync(
			@event: BuildRevokedEvent(userId: Guid.CreateVersion7(), permission: "budget:write"),
			ct: CancellationToken.None
		)).ThrowsNothing();
	}

	[Test]
	public async Task RevokeAsync_ShouldOnlyAffectMatchingPermission()
	{
		Guid userId = Guid.CreateVersion7();
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:read"), ct: CancellationToken.None);
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:write"), ct: CancellationToken.None);

		await _repository.RevokeAsync(@event: BuildRevokedEvent(userId: userId, permission: "account:read"), ct: CancellationToken.None);

		await Assert.That(value: await CountActiveAsync(userId: userId)).IsEqualTo(expected: 1);

		UserPermissionEntity? remaining = await FindAsync(userId: userId, permission: "account:write");
		await Assert.That(value: remaining!.IsActive).IsTrue();
	}

	[Test]
	public async Task GrantAsync_OutOfOrderAfterARevoke_ShouldNotResurrectThePermission()
	{
		Guid userId = Guid.CreateVersion7();
		const string permission = "account:write";

		await _repository.RevokeAsync(@event: BuildRevokedEvent(userId: userId, permission: permission, version: 3), ct: CancellationToken.None);
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: permission, version: 2), ct: CancellationToken.None);

		UserPermissionEntity? row = await FindAsync(userId: userId, permission: permission);

		await Assert.That(value: row!.IsActive).IsFalse().Because(message: """
			Delayed retry lets a grant arrive after the revoke that superseded it. Applying it would hand
			back access that was deliberately taken away — with nothing in the logs to show for it.
		""");
		await Assert.That(value: row.LastVersion).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task RevokeAsync_OutOfOrderAfterALaterGrant_ShouldNotRemoveThePermission()
	{
		Guid userId = Guid.CreateVersion7();
		const string permission = "budget:write";

		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: permission, version: 5), ct: CancellationToken.None);
		await _repository.RevokeAsync(@event: BuildRevokedEvent(userId: userId, permission: permission, version: 4), ct: CancellationToken.None);

		UserPermissionEntity? row = await FindAsync(userId: userId, permission: permission);

		await Assert.That(value: row!.IsActive).IsTrue().Because(message: """
			The grant is the newer fact. A stale revoke overtaking it would silently lock the user out of
			something they were just given.
		""");
		await Assert.That(value: row.LastVersion).IsEqualTo(expected: 5);
	}

	[Test]
	public async Task DeleteOldTombstonesAsync_ShouldRemoveOnlyExpiredOnes()
	{
		Guid userId = Guid.CreateVersion7();
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:read"), ct: CancellationToken.None);
		await _repository.RevokeAsync(@event: BuildRevokedEvent(userId: userId, permission: "account:read"), ct: CancellationToken.None);

		int deleted = await _repository.DeleteOldTombstonesAsync(
			before: FakeDateProvider.Default.UtcNow.AddDays(days: 1),
			batchSize: 100,
			ct: CancellationToken.None
		);

		await Assert.That(value: deleted).IsEqualTo(expected: 1);
		await Assert.That(value: await FindAsync(userId: userId, permission: "account:read")).IsNull();
	}

	[Test]
	public async Task DeleteOldTombstonesAsync_ShouldKeepRecentOnes()
	{
		Guid userId = Guid.CreateVersion7();
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "budget:read"), ct: CancellationToken.None);
		await _repository.RevokeAsync(@event: BuildRevokedEvent(userId: userId, permission: "budget:read"), ct: CancellationToken.None);

		int deleted = await _repository.DeleteOldTombstonesAsync(
			before: FakeDateProvider.Default.UtcNow.AddDays(days: -1),
			batchSize: 100,
			ct: CancellationToken.None
		);

		await Assert.That(value: deleted).IsEqualTo(expected: 0);
		await Assert.That(value: await FindAsync(userId: userId, permission: "budget:read")).IsNotNull().Because(message: """
			Deleting a tombstone before the broker has forgotten the message means a replayed revoke or
			grant lands on an empty table and gets applied as if it were new.
		""");
	}

	[Test]
	public async Task DeleteOldTombstonesAsync_ShouldNotTouchLiveRows()
	{
		Guid userId = Guid.CreateVersion7();
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "category:read"), ct: CancellationToken.None);

		int deleted = await _repository.DeleteOldTombstonesAsync(
			before: FakeDateProvider.Default.UtcNow.AddDays(days: 1),
			batchSize: 100,
			ct: CancellationToken.None
		);

		await Assert.That(value: deleted).IsEqualTo(expected: 0);
	}

		[Test]
	public async Task DeleteAllForUserAsync_ShouldRemoveActiveRowsAndTombstonesAlike()
	{
		Guid userId = Guid.CreateVersion7();

		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:read"), ct: CancellationToken.None);
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:write"), ct: CancellationToken.None);
		await _repository.RevokeAsync(@event: BuildRevokedEvent(userId: userId, permission: "account:write"), ct: CancellationToken.None);

		await _repository.DeleteAllForUserAsync(userId: userId, ct: CancellationToken.None);

		int remaining = await Context.UserPermissions.CountAsync(predicate: e => e.UserId == userId);

		await Assert.That(value: remaining).IsEqualTo(expected: 0).Because(message: """
			Tombstones have to go with the live rows. A replay starts at the first event, and a leftover row
			already carrying the latest version makes every version-guarded write bounce off it — the rebuild
			would then report success having applied nothing.
		""");
	}

	[Test]
	public async Task DeleteAllForUserAsync_ShouldLeaveOtherUsersAlone()
	{
		Guid userId = Guid.CreateVersion7();
		Guid otherUserId = Guid.CreateVersion7();

		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:read"), ct: CancellationToken.None);
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: otherUserId, permission: "account:read"), ct: CancellationToken.None);

		await _repository.DeleteAllForUserAsync(userId: userId, ct: CancellationToken.None);

		await Assert.That(value: await FindAsync(userId: userId, permission: "account:read")).IsNull();
		await Assert.That(value: await FindAsync(userId: otherUserId, permission: "account:read")).IsNotNull().Because(message: """
			Rebuilds run one aggregate at a time and in parallel with others. A clear that reached past its
			own user would wipe read models nobody asked to rebuild.
		""");
	}

	[Test]
	public async Task DeleteAllForUserAsync_ThenReplayingTheSameEvent_ShouldRestoreThePermission()
	{
		Guid userId = Guid.CreateVersion7();
		const string permission = "account:write";

		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: permission), ct: CancellationToken.None);

		await _repository.DeleteAllForUserAsync(userId: userId, ct: CancellationToken.None);
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: permission), ct: CancellationToken.None);

		UserPermissionEntity? row = await FindAsync(userId: userId, permission: permission);

		await Assert.That(value: row!.IsActive).IsTrue().Because(message: """
			This pair is what makes a rebuild possible here at all. Version-guarded writes are the reason the
			out-of-order tests above pass, and the same guard would reject an honest replay — clearing first
			is what tells the two apart.
		""");
		await Assert.That(value: row.LastVersion).IsEqualTo(expected: 1);
	}
}
