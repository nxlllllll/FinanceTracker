using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinanceTracker.Api.Http;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class CurrentUserProviderTests
{
	private static CurrentUserProvider ProviderFor(HttpContext? httpContext)
	{
		IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
		accessor.HttpContext.Returns(returnThis: httpContext);

		return new CurrentUserProvider(httpContextAccessor: accessor);
	}

	private static HttpContext ContextWith(params Claim[] claims) => new DefaultHttpContext
	{
		User = new ClaimsPrincipal(identity: new ClaimsIdentity(claims: claims, authenticationType: "Test"))
	};

	[Test]
	public async Task TheSubjectClaimBecomesTheUserId()
	{
		Guid userId = Guid.CreateVersion7();

		CurrentUserProvider provider = ProviderFor(httpContext: ContextWith(
			new Claim(type: JwtRegisteredClaimNames.Sub, value: userId.ToString()),
			new Claim(type: JwtRegisteredClaimNames.Sid, value: Guid.CreateVersion7().ToString())
		));

		await Assert.That(value: provider.UserId).IsEqualTo(expected: userId);
	}

	[Test]
	public async Task TheSessionClaimBecomesTheSessionId()
	{
		Guid sessionId = Guid.CreateVersion7();

		CurrentUserProvider provider = ProviderFor(httpContext: ContextWith(
			new Claim(type: JwtRegisteredClaimNames.Sub, value: Guid.CreateVersion7().ToString()),
			new Claim(type: JwtRegisteredClaimNames.Sid, value: sessionId.ToString())
		));

		await Assert.That(value: provider.SessionId).IsEqualTo(expected: sessionId);
	}

	[Test]
	public async Task AMissingClaimIsRefusedRatherThanDefaulted()
	{
		CurrentUserProvider provider = ProviderFor(httpContext: ContextWith(
			new Claim(type: JwtRegisteredClaimNames.Sid, value: Guid.CreateVersion7().ToString())
		));

		await Assert.That(action: () => _ = provider.UserId).Throws<InvalidOperationException>()
			.Because(message: "an empty id is a valid filter value, so returning one would run the command against no user at all");
	}

	[Test]
	public async Task AClaimThatIsNotAnIdentifierIsRefused()
	{
		CurrentUserProvider provider = ProviderFor(httpContext: ContextWith(
			new Claim(type: JwtRegisteredClaimNames.Sub, value: "not-a-guid")
		));

		await Assert.That(action: () => _ = provider.UserId).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task NoRequestAtAllIsRefused()
	{
		CurrentUserProvider provider = ProviderFor(httpContext: null);

		await Assert.That(action: () => _ = provider.UserId).Throws<InvalidOperationException>();
		await Assert.That(action: () => _ = provider.SessionId).Throws<InvalidOperationException>();
	}
}
