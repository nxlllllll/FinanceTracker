using System.Security.Claims;
using FinanceTracker.Api.Auth;
using FinanceTracker.Core.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Auth;

public sealed class RootAuthorizationHandlerTests
{
	private static AuthorizationHandlerContext BuildContext(
		Guid? userId,
		RootRequirement requirement)
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
	public async Task HandleAsync_WhenUserIsRoot_ShouldSucceed()
	{
		Guid userId = Guid.CreateVersion7();
		IRootAuthority rootAuthority = Substitute.For<IRootAuthority>();
		rootAuthority.IsRootAsync(userId: userId).Returns(returnThis: true);

		RootAuthorizationHandler handler = new RootAuthorizationHandler(rootAuthority: rootAuthority);
		AuthorizationHandlerContext context = BuildContext(
			userId: userId,
			requirement: new RootRequirement()
		);

		await handler.HandleAsync(context: context);

		await Assert.That(value: context.HasSucceeded).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenUserIsNotRoot_ShouldNotSucceed()
	{
		Guid userId = Guid.CreateVersion7();
		IRootAuthority rootAuthority = Substitute.For<IRootAuthority>();
		rootAuthority.IsRootAsync(userId: userId).Returns(returnThis: false);

		RootAuthorizationHandler handler = new RootAuthorizationHandler(rootAuthority: rootAuthority);
		AuthorizationHandlerContext context = BuildContext(
			userId: userId,
			requirement: new RootRequirement()
		);

		await handler.HandleAsync(context: context);

		await Assert.That(value: context.HasSucceeded).IsFalse();
	}

	[Test]
	public async Task HandleAsync_WithMissingSubClaim_ShouldNotSucceedAndNotThrow()
	{
		IRootAuthority rootAuthority = Substitute.For<IRootAuthority>();

		RootAuthorizationHandler handler = new RootAuthorizationHandler(rootAuthority: rootAuthority);
		AuthorizationHandlerContext context = BuildContext(
			userId: null,
			requirement: new RootRequirement()
		);

		await Assert.That(action: async () => await handler.HandleAsync(context: context)).ThrowsNothing();
		await Assert.That(value: context.HasSucceeded).IsFalse();
	}
}
