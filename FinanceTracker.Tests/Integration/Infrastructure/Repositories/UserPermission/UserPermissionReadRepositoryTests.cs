using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Infrastructure.Database.Repositories.UserPermission;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.UserPermission;

public sealed class UserPermissionReadRepositoryTests : DatabaseFixture
{
	private UserPermissionReadRepository _readRepository = null!;
	private UserPermissionWriteRepository _writeRepository = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = new UserPermissionReadRepository(context: Context);
		_writeRepository = new UserPermissionWriteRepository(context: Context);
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
