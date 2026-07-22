using FinanceTracker.Core.Domains.UserPermission.Events;
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

	private static PermissionGranted BuildGrantedEvent(Guid userId, string permission) => new PermissionGranted(
		Id: Guid.CreateVersion7(),
		UserId: userId,
		GrantedBy: Guid.CreateVersion7(),
		Permission: permission,
		Version: 1,
		OccurredAt: FakeDateProvider.Default.UtcNow
	);

	[Test]
	public async Task GrantAsync_ShouldInsertRow()
	{
		Guid userId = Guid.CreateVersion7();

		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:write"), ct: CancellationToken.None);

		FinanceTracker.Infrastructure.Database.Context.UserPermission.UserPermissionEntity? row =
			await Context.UserPermissions.FindAsync(userId, "account:write");

		await Assert.That(value: row).IsNotNull();
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

		int count = await Context.UserPermissions.CountAsync(predicate: e => e.UserId == userId);
		await Assert.That(value: count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task RevokeAsync_WithExistingRow_ShouldDeleteIt()
	{
		Guid userId = Guid.CreateVersion7();
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "transaction:delete"), ct: CancellationToken.None);

		await _repository.RevokeAsync(userId: userId, permission: "transaction:delete", ct: CancellationToken.None);

		FinanceTracker.Infrastructure.Database.Context.UserPermission.UserPermissionEntity? row = await Context.UserPermissions.FindAsync(userId, "transaction:delete");
		await Assert.That(value: row).IsNull();
	}

	[Test]
	public async Task RevokeAsync_WithNoExistingRow_ShouldNotThrow()
	{
		await Assert.That(action: async () => await _repository.RevokeAsync(
			userId: Guid.CreateVersion7(),
			permission: "budget:write",
			ct: CancellationToken.None
		)).ThrowsNothing();
	}

	[Test]
	public async Task RevokeAsync_ShouldOnlyAffectMatchingPermission()
	{
		Guid userId = Guid.CreateVersion7();
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:read"), ct: CancellationToken.None);
		await _repository.GrantAsync(@event: BuildGrantedEvent(userId: userId, permission: "account:write"), ct: CancellationToken.None);

		await _repository.RevokeAsync(userId: userId, permission: "account:read", ct: CancellationToken.None);

		int count = await Context.UserPermissions.CountAsync(predicate: e => e.UserId == userId);
		await Assert.That(value: count).IsEqualTo(expected: 1);
		FinanceTracker.Infrastructure.Database.Context.UserPermission.UserPermissionEntity? remaining = await Context.UserPermissions.FindAsync(userId, "account:write");
		await Assert.That(value: remaining).IsNotNull();
	}
}
