using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.Role;
using FinanceTracker.Infrastructure.Database.Repositories.UserRole;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Role;

public sealed class RoleRepositoryTombstoneTests : DatabaseFixture
{
	private RoleRepository _roleRepository = null!;
	private UserRoleWriteRepository _membershipWriter = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_roleRepository = new RoleRepository(context: Context);
		_membershipWriter = new UserRoleWriteRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private Task<Guid> SeededRoleIdAsync(SystemRole systemKey)
	{
		return Context.Roles.AsNoTracking().Where(predicate: r => r.SystemKey == systemKey).Select(selector: r => r.Id).FirstAsync();
	}

	private async Task AssignThenRemoveAsync(Guid userId, Guid roleId)
	{
		await _membershipWriter.AssignAsync(@event: new RoleAssigned(
			Id: Guid.CreateVersion7(),
			UserId: userId,
			RoleId: roleId,
			AssignedBy: Guid.CreateVersion7(),
			Version: 2,
			OccurredAt: FakeDateProvider.Default.UtcNow
		), ct: CancellationToken.None);

		await _membershipWriter.RemoveAsync(@event: new RoleRemoved(
			Id: Guid.CreateVersion7(),
			UserId: userId,
			RoleId: roleId,
			RemovedBy: Guid.CreateVersion7(),
			Version: 3,
			OccurredAt: FakeDateProvider.Default.UtcNow
		), ct: CancellationToken.None);
	}

	[Test]
	public async Task GetByUserIdAsync_ShouldNotReportARemovedRole()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);
		await AssignThenRemoveAsync(userId: userId, roleId: roleId);

		IReadOnlyList<RoleDto> roles = await _roleRepository.GetByUserIdAsync(userId: userId, ct: CancellationToken.None);

		await Assert.That(value: roles).IsEmpty();
	}

	[Test]
	public async Task GetMemberUserIdsAsync_ShouldNotReportAFormerMember()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.User);
		await AssignThenRemoveAsync(userId: userId, roleId: roleId);

		IReadOnlyList<Guid> members = await _roleRepository.GetMemberUserIdsAsync(roleId: roleId, ct: CancellationToken.None);

		await Assert.That(value: members).DoesNotContain(expected: userId);
	}

	[Test]
	public async Task CountMembersWithSystemKeyAsync_ShouldNotCountTombstonedMembership()
	{
		Guid formerRoot = await _userBuilder.CreateAsync();
		Guid roleId = await SeededRoleIdAsync(systemKey: SystemRole.Root);
		await AssignThenRemoveAsync(userId: formerRoot, roleId: roleId);

		int rootHolders = await _roleRepository.CountMembersWithSystemKeyAsync(systemKey: SystemRole.Root, ct: CancellationToken.None);

		await Assert.That(value: rootHolders).IsEqualTo(expected: 0).Because(message: """
			This count is what stops the last root role from being removed. Counting tombstones as holders
			would let the real last one go — and there is no path back to root once nobody has it.
		""");
	}
}
