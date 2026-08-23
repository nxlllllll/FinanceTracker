using FinanceTracker.Api.Endpoints.Auth.Commands;
using FinanceTracker.Application.UseCases.User.Commands.RevokeToken;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

public sealed class LogoutEndpointTests
{
	private const string CookieName = "__Host-refresh-token";

	private static HttpContext ContextWithCookie(Stream body, string? refreshToken)
	{
		HttpContext context = HttpContextFactory.Create(body: body, method: "POST", path: "/api/v1/auth/logout");

		if (refreshToken is not null)
			context.Request.Headers.Cookie = $"{CookieName}={refreshToken}";

		return context;
	}

	private static string? SetCookieHeader(HttpContext context)
	{
		return context.Response.Headers.SetCookie.FirstOrDefault(
			predicate: header => header?.StartsWith(value: CookieName, comparisonType: StringComparison.Ordinal) == true
		);
	}

	[Test]
	public async Task HandleAsync_WithACookie_ShouldRevokeThatSession()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = ContextWithCookie(body: body, refreshToken: "the-token");

		ISender sender = Substitute.For<ISender>();

		await LogoutEndpoint.HandleAsync(sender: sender, httpContext: context, ct: CancellationToken.None);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RevokeTokenCommand>(predicate: command => command!.RefreshToken == "the-token"),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithoutACookie_ShouldNotRevokeAnything()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = ContextWithCookie(body: body, refreshToken: null);

		ISender sender = Substitute.For<ISender>();

		await LogoutEndpoint.HandleAsync(sender: sender, httpContext: context, ct: CancellationToken.None);

		await sender.DidNotReceive().Send(request: Arg.Any<RevokeTokenCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WithoutACookie_ShouldStillClearIt()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = ContextWithCookie(body: body, refreshToken: null);

		ISender sender = Substitute.For<ISender>();

		await LogoutEndpoint.HandleAsync(sender: sender, httpContext: context, ct: CancellationToken.None);

		await Assert.That(value: SetCookieHeader(context: context)).IsNotNull().Because(message: """
			Nothing to revoke does not mean nothing to clean up: the browser may hold a cookie this
			server no longer recognises, and logout is the one call that reliably gets rid of it.
		""");
	}

	[Test]
	public async Task HandleAsync_WithACookie_ShouldClearIt()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = ContextWithCookie(body: body, refreshToken: "the-token");

		ISender sender = Substitute.For<ISender>();

		await LogoutEndpoint.HandleAsync(sender: sender, httpContext: context, ct: CancellationToken.None);

		await Assert.That(value: SetCookieHeader(context: context)).IsNotNull();
	}
}
