using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Infrastructure.Services.Auth;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class RoleBasedRootAuthorityTests
{
	[Test]
	public async Task IsRootAsync_WithEmptyGuid_ShouldReturnFalseWithoutCallingRepository()
	{
		IUserRoleReadRepository userRoleReadRepository = Substitute.For<IUserRoleReadRepository>();
		RoleBasedRootAuthority authority = new RoleBasedRootAuthority(userRoleReadRepository: userRoleReadRepository);

		bool result = await authority.IsRootAsync(userId: Guid.Empty);

		await Assert.That(value: result).IsFalse();
		await userRoleReadRepository.DidNotReceive().HasSystemRoleAsync(
			userId: Arg.Any<Guid>(),
			systemKey: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task IsRootAsync_WhenUserHasRootRole_ShouldReturnTrue()
	{
		Guid userId = Guid.CreateVersion7();
		IUserRoleReadRepository userRoleReadRepository = Substitute.For<IUserRoleReadRepository>();
		userRoleReadRepository.HasSystemRoleAsync(
			userId: userId,
			systemKey: "root",
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		RoleBasedRootAuthority authority = new RoleBasedRootAuthority(userRoleReadRepository: userRoleReadRepository);

		bool result = await authority.IsRootAsync(userId: userId);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task IsRootAsync_WhenUserLacksRootRole_ShouldReturnFalse()
	{
		Guid userId = Guid.CreateVersion7();
		IUserRoleReadRepository userRoleReadRepository = Substitute.For<IUserRoleReadRepository>();
		userRoleReadRepository.HasSystemRoleAsync(
			userId: userId,
			systemKey: "root",
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		RoleBasedRootAuthority authority = new RoleBasedRootAuthority(userRoleReadRepository: userRoleReadRepository);

		bool result = await authority.IsRootAsync(userId: userId);

		await Assert.That(value: result).IsFalse();
	}
}
