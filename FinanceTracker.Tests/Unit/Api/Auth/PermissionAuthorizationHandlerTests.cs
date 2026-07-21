using System.Security.Claims;
using FinanceTracker.Api.Auth;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Auth;

public sealed class PermissionAuthorizationHandlerTests
{
	private static AuthorizationHandlerContext BuildContext(
		Guid? userId,
		PermissionRequirement requirement)
	{
		ClaimsIdentity identity = userId is null ? new ClaimsIdentity() : new ClaimsIdentity(claims: [
			new Claim(type: JwtRegisteredClaimNames.Sub, value: userId.Value.ToString())
		]);

		return new AuthorizationHandlerContext(
			requirements: [requirement],
			user: new ClaimsPrincipal(identity: identity),
			resource: null
		);
	}

	[Test]
	public async Task HandleAsync_WhenUserIsRoot_ShouldSucceedWithoutReadingPermissions()
	{
		Guid userId = Guid.CreateVersion7();
		IUserPermissionReadRepository permissionReadRepository = Substitute.For<IUserPermissionReadRepository>();
		IRootAuthority rootAuthority = Substitute.For<IRootAuthority>();
		rootAuthority.IsRootAsync(userId: userId).Returns(returnThis: true);

		PermissionAuthorizationHandler handler = new PermissionAuthorizationHandler(
			permissionReadRepository: permissionReadRepository,
			rootAuthority: rootAuthority
		);
		PermissionRequirement requirement = new PermissionRequirement(permission: "account:write");
		AuthorizationHandlerContext context = BuildContext(
			userId: userId,
			requirement: requirement
		);

		await handler.HandleAsync(context: context);

		await Assert.That(value: context.HasSucceeded).IsTrue();
		await permissionReadRepository.DidNotReceive().GetPermissionsAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenNotRootAndHasPermission_ShouldSucceed()
	{
		Guid userId = Guid.CreateVersion7();
		IUserPermissionReadRepository permissionReadRepository = Substitute.For<IUserPermissionReadRepository>();
		permissionReadRepository.GetPermissionsAsync(
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new HashSet<string> { "account:write" });
		IRootAuthority rootAuthority = Substitute.For<IRootAuthority>();
		rootAuthority.IsRootAsync(userId: userId).Returns(returnThis: false);

		PermissionAuthorizationHandler handler = new PermissionAuthorizationHandler(
			permissionReadRepository: permissionReadRepository,
			rootAuthority: rootAuthority
		);
		PermissionRequirement requirement = new PermissionRequirement(permission: "account:write");
		AuthorizationHandlerContext context = BuildContext(
			userId: userId,
			requirement: requirement
		);

		await handler.HandleAsync(context: context);

		await Assert.That(value: context.HasSucceeded).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenNotRootAndLacksPermission_ShouldNotSucceed()
	{
		Guid userId = Guid.CreateVersion7();
		IUserPermissionReadRepository permissionReadRepository = Substitute.For<IUserPermissionReadRepository>();
		permissionReadRepository.GetPermissionsAsync(
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new HashSet<string>());
		IRootAuthority rootAuthority = Substitute.For<IRootAuthority>();
		rootAuthority.IsRootAsync(userId: userId).Returns(returnThis: false);

		PermissionAuthorizationHandler handler = new PermissionAuthorizationHandler(
			permissionReadRepository: permissionReadRepository,
			rootAuthority: rootAuthority
		);
		PermissionRequirement requirement = new PermissionRequirement(permission: "account:write");
		AuthorizationHandlerContext context = BuildContext(
			userId: userId,
			requirement: requirement
		);

		await handler.HandleAsync(context: context);

		await Assert.That(value: context.HasSucceeded).IsFalse();
	}

	[Test]
	public async Task HandleAsync_WithMissingSubClaim_ShouldNotSucceedAndNotThrow()
	{
		IUserPermissionReadRepository permissionReadRepository = Substitute.For<IUserPermissionReadRepository>();
		IRootAuthority rootAuthority = Substitute.For<IRootAuthority>();

		PermissionAuthorizationHandler handler = new PermissionAuthorizationHandler(
			permissionReadRepository: permissionReadRepository,
			rootAuthority: rootAuthority
		);
		PermissionRequirement requirement = new PermissionRequirement(permission: "account:write");
		AuthorizationHandlerContext context = BuildContext(
			userId: null,
			requirement: requirement
		);

		await Assert.That(action: async () => await handler.HandleAsync(context: context)).ThrowsNothing();
		await Assert.That(value: context.HasSucceeded).IsFalse();
	}
}
