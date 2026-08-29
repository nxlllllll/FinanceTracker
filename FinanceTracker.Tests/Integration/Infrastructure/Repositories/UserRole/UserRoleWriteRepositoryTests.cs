using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Role;
using FinanceTracker.Infrastructure.Database.Repositories.Role;
using FinanceTracker.Infrastructure.Database.Repositories.UserRole;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.UserRole;

public sealed class UserRoleWriteRepositoryTests : DatabaseFixture
{
	private UserRoleWriteRepository _repository = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_repository = new UserRoleWriteRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private Task<Guid> SeededRoleIdAsync(SystemRole systemKey)
	{
		return Context.Roles.AsNoTracking()
			.Where(predicate: r => r.SystemKey == systemKey)
			.Select(selector: r => r.Id)
			.FirstAsync();
	}

	private static RoleAssigned BuildAssignedEvent(
		Guid userId,
		Guid roleId,
		int version = 2
	) => new RoleAssigned(
		Id: Guid.CreateVersion7(),
		UserId: userId,
		RoleId: roleId,
		AssignedBy: Guid.CreateVersion7(),
		Version: version,
		OccurredAt: FakeDateProvider.Default.UtcNow
	);

	private static RoleRemoved BuildRemovedEvent(
		Guid userId,
		Guid roleId,
		int version = 3
	) => new RoleRemoved(
		Id: Guid.CreateVersion7(),
		UserId: userId,
		RoleId: roleId,
		RemovedBy: Guid.CreateVersion7(),
		Version: version,
		OccurredAt: FakeDateProvider.Default.UtcNow
	);

	private Task<UserRoleEntity?> FindAsync(
		Guid userId,
		Guid roleId
	) => Context.UserRoles.AsNoTracking().FirstOrDefaultAsync(predicate: e => e.UserId == userId && e.RoleId == roleId);

	[Test]
	public async Task AssignAsync_ShouldInsertActiveRowWithItsAuthor()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);
		RoleAssigned @event = BuildAssignedEvent(userId: userId, roleId: roleId);

		await _repository.AssignAsync(@event: @event, ct: CancellationToken.None);

		UserRoleEntity? row = await FindAsync(userId: userId, roleId: roleId);

		await Assert.That(value: row).IsNotNull();
		await Assert.That(value: row!.IsActive).IsTrue();
		await Assert.That(value: row.LastVersion).IsEqualTo(expected: 2);
		await Assert.That(value: row.AssignedBy).IsEqualTo(expected: @event.AssignedBy);
	}

	[Test]
	public async Task AssignAsync_CalledTwiceWithSameEvent_ShouldNotThrowAndShouldStayOneRow()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);
		RoleAssigned @event = BuildAssignedEvent(userId: userId, roleId: roleId);

		await Assert.That(action: async () =>
		{
			await _repository.AssignAsync(@event: @event, ct: CancellationToken.None);
			await _repository.AssignAsync(@event: @event, ct: CancellationToken.None);
		}).ThrowsNothing();

		int count = await Context.UserRoles.CountAsync(predicate: e => e.UserId == userId);
		await Assert.That(value: count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task RemoveAsync_WithExistingRow_ShouldLeaveATombstoneCarryingTheAudit()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);
		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId), ct: CancellationToken.None);

		RoleRemoved removal = BuildRemovedEvent(userId: userId, roleId: roleId);
		await _repository.RemoveAsync(@event: removal, ct: CancellationToken.None);

		UserRoleEntity? row = await FindAsync(userId: userId, roleId: roleId);

		await Assert.That(value: row).IsNotNull();
		await Assert.That(value: row!.IsActive).IsFalse();
		await Assert.That(value: row.LastVersion).IsEqualTo(expected: 3);
		await Assert.That(value: row.RemovedBy).IsEqualTo(expected: removal.RemovedBy);
		await Assert.That(value: row.RemovedAt).IsNotNull();
	}

	[Test]
	public async Task RemoveAsync_WithNoExistingRow_ShouldNotThrow()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.Admin);

		await Assert.That(action: async () => await _repository.RemoveAsync(
			@event: BuildRemovedEvent(userId: userId, roleId: roleId),
			ct: CancellationToken.None
		)).ThrowsNothing();
	}

	[Test]
	public async Task AssignAsync_AfterARemoval_ShouldRestoreMembershipAndClearTheRemovalAudit()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);

		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId, version: 2), ct: CancellationToken.None);
		await _repository.RemoveAsync(@event: BuildRemovedEvent(userId: userId, roleId: roleId, version: 3), ct: CancellationToken.None);
		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId, version: 4), ct: CancellationToken.None);

		UserRoleEntity? row = await FindAsync(userId: userId, roleId: roleId);

		await Assert.That(value: row!.IsActive).IsTrue().Because(message: """
			Being given a role back is an ordinary thing to happen, and the tombstone must not stand in
			its way — only stale events are meant to bounce off it.
		""");
		await Assert.That(value: row.RemovedAt).IsNull();
		await Assert.That(value: row.RemovedBy).IsNull();
	}

	[Test]
	public async Task AssignAsync_OutOfOrderAfterARemoval_ShouldNotRestoreMembership()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.Root);

		await _repository.RemoveAsync(@event: BuildRemovedEvent(userId: userId, roleId: roleId, version: 5), ct: CancellationToken.None);
		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId, version: 4), ct: CancellationToken.None);

		UserRoleEntity? row = await FindAsync(userId: userId, roleId: roleId);

		await Assert.That(value: row!.IsActive).IsFalse().Because(message: """
			A stale assignment slipping through would hand back every permission the role carries — and
			for the root role, the authorization bypass along with them.
		""");
		await Assert.That(value: row.LastVersion).IsEqualTo(expected: 5);
	}

	[Test]
	public async Task RemoveAsync_OutOfOrderAfterALaterAssignment_ShouldNotDropMembership()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);

		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId, version: 7), ct: CancellationToken.None);
		await _repository.RemoveAsync(@event: BuildRemovedEvent(userId: userId, roleId: roleId, version: 6), ct: CancellationToken.None);

		UserRoleEntity? row = await FindAsync(userId: userId, roleId: roleId);

		await Assert.That(value: row!.IsActive).IsTrue().Because(message: """
			The assignment is the newer fact. A stale removal overtaking it would lock the user out of
			everything the role grants, with nothing to explain why.
		""");
		await Assert.That(value: row.LastVersion).IsEqualTo(expected: 7);
	}

	[Test]
	public async Task HasSystemRoleAsync_ShouldIgnoreTombstonedMembership()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.Root);
		UserRoleReadRepository readRepository = new UserRoleReadRepository(context: Context);

		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId, version: 2), ct: CancellationToken.None);
		await _repository.RemoveAsync(@event: BuildRemovedEvent(userId: userId, roleId: roleId, version: 3), ct: CancellationToken.None);

		bool hasRoot = await readRepository.HasSystemRoleAsync(userId: userId, systemKey: SystemRole.Root, ct: CancellationToken.None);

		await Assert.That(value: hasRoot).IsFalse().Because(message: """
			The root role skips ownership checks entirely, so a tombstone that still reads as membership
			is not a stale row — it is a user who cannot be stripped of admin access.
		""");
	}
	[Test]
	public async Task DeleteOldTombstonesAsync_ShouldRemoveOnlyExpiredOnes()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);
		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId), ct: CancellationToken.None);
		await _repository.RemoveAsync(@event: BuildRemovedEvent(userId: userId, roleId: roleId), ct: CancellationToken.None);

		int deleted = await _repository.DeleteOldTombstonesAsync(
			before: FakeDateProvider.Default.UtcNow.AddDays(days: 1),
			batchSize: 100,
			ct: CancellationToken.None
		);

		await Assert.That(value: deleted).IsEqualTo(expected: 1);
		await Assert.That(value: await FindAsync(userId: userId, roleId: roleId)).IsNull();
	}

	[Test]
	public async Task DeleteOldTombstonesAsync_ShouldKeepRecentOnes()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.Admin);
		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId), ct: CancellationToken.None);
		await _repository.RemoveAsync(@event: BuildRemovedEvent(userId: userId, roleId: roleId), ct: CancellationToken.None);

		int deleted = await _repository.DeleteOldTombstonesAsync(
			before: FakeDateProvider.Default.UtcNow.AddDays(days: -1),
			batchSize: 100,
			ct: CancellationToken.None
		);

		await Assert.That(value: deleted).IsEqualTo(expected: 0);
		await Assert.That(value: await FindAsync(userId: userId, roleId: roleId)).IsNotNull().Because(message: """
			Deleting a tombstone while the broker can still redeliver the assignment it superseded means
			that assignment lands on an empty table and restores membership that was taken away.
		""");
	}

	[Test]
	public async Task DeleteOldTombstonesAsync_ShouldNotTouchLiveMembership()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);
		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId), ct: CancellationToken.None);

		int deleted = await _repository.DeleteOldTombstonesAsync(
			before: FakeDateProvider.Default.UtcNow.AddDays(days: 1),
			batchSize: 100,
			ct: CancellationToken.None
		);

		await Assert.That(value: deleted).IsEqualTo(expected: 0);
		await Assert.That(value: (await FindAsync(userId: userId, roleId: roleId))!.IsActive).IsTrue();
	}

		[Test]
	public async Task DeleteAllForUserAsync_ShouldRemoveLiveMembershipAndTombstonesAlike()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid keptRoleId = await SeededRoleIdAsync(systemKey: SystemRole.User);
		Guid removedRoleId = await SeededRoleIdAsync(systemKey: SystemRole.Admin);

		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: keptRoleId, version: 2), ct: CancellationToken.None);
		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: removedRoleId, version: 3), ct: CancellationToken.None);
		await _repository.RemoveAsync(@event: BuildRemovedEvent(userId: userId, roleId: removedRoleId, version: 4), ct: CancellationToken.None);

		await _repository.DeleteAllForUserAsync(userId: userId, ct: CancellationToken.None);

		int remaining = await Context.UserRoles.CountAsync(predicate: e => e.UserId == userId);

		await Assert.That(value: remaining).IsEqualTo(expected: 0).Because(message: """
			Tombstones have to go with the live rows. A replay starts at the first event, and a leftover row
			already carrying the latest version makes every version-guarded write bounce off it — the rebuild
			would then report success having applied nothing.
		""");
	}

	[Test]
	public async Task DeleteAllForUserAsync_ShouldLeaveOtherUsersAlone()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid otherUserId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);

		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId), ct: CancellationToken.None);
		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: otherUserId, roleId: roleId), ct: CancellationToken.None);

		await _repository.DeleteAllForUserAsync(userId: userId, ct: CancellationToken.None);

		await Assert.That(value: await FindAsync(userId: userId, roleId: roleId)).IsNull();
		await Assert.That(value: await FindAsync(userId: otherUserId, roleId: roleId)).IsNotNull().Because(message: """
			Rebuilds run one aggregate at a time and in parallel with others. A clear that reached past its
			own user would wipe read models nobody asked to rebuild.
		""");
	}

	[Test]
	public async Task DeleteAllForUserAsync_ThenReplayingTheSameEvent_ShouldRestoreMembership()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);

		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId, version: 2), ct: CancellationToken.None);

		await _repository.DeleteAllForUserAsync(userId: userId, ct: CancellationToken.None);
		await _repository.AssignAsync(@event: BuildAssignedEvent(userId: userId, roleId: roleId, version: 2), ct: CancellationToken.None);

		UserRoleEntity? row = await FindAsync(userId: userId, roleId: roleId);

		await Assert.That(value: row!.IsActive).IsTrue().Because(message: """
			This pair is what makes a rebuild possible here at all. Version-guarded writes are the reason the
			out-of-order tests above pass, and the same guard would reject an honest replay — clearing first
			is what tells the two apart.
		""");
		await Assert.That(value: row.LastVersion).IsEqualTo(expected: 2);
	}
}
