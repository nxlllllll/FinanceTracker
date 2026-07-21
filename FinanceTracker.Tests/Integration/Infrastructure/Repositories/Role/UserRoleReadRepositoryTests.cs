using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.Role;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Role;

public sealed class UserRoleReadRepositoryTests : DatabaseFixture
{
	private UserRoleReadRepository _readRepository = null!;
	private RoleRepository _roleRepository = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = new UserRoleReadRepository(context: Context);
		_roleRepository = new RoleRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	[Test]
	public async Task HasSystemRoleAsync_WhenUserHoldsRole_ShouldReturnTrue()
	{
		RoleDto? rootRole = await _roleRepository.GetBySystemKeyAsync(systemKey: SystemRole.Root, ct: CancellationToken.None);
		Guid userId = await _userBuilder.CreateAsync();;
		await _roleRepository.AssignToUserAsync(
			userId: userId,
			roleId: rootRole!.Id,
			assignedAt: FakeDateProvider.Default.UtcNow,
			ct: CancellationToken.None
		);

		bool hasRole = await _readRepository.HasSystemRoleAsync(
			userId: userId,
			systemKey: SystemRole.Root,
			ct: CancellationToken.None
		);

		await Assert.That(value: hasRole).IsTrue();
	}

	[Test]
	public async Task HasSystemRoleAsync_WhenUserDoesNotHoldRole_ShouldReturnFalse()
	{
		bool hasRole = await _readRepository.HasSystemRoleAsync(
			userId: Guid.CreateVersion7(),
			systemKey: SystemRole.Root,
			ct: CancellationToken.None
		);

		await Assert.That(value: hasRole).IsFalse();
	}
}
