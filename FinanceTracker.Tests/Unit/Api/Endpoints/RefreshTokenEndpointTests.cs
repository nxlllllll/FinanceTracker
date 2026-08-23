using FinanceTracker.Api.Endpoints.Auth.Commands;
using FinanceTracker.Application.UseCases.User.Commands.RefreshToken;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Auth;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Infrastructure.Services.Token;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Endpoints;

/// <summary>
/// Covers the rotation path: the cookie is both the input and the output, and the endpoint decides
/// whether the caller leaves with a new one or with none at all.
/// </summary>
public sealed class RefreshTokenEndpointTests
{
	private const string CookieName = "__Host-refresh-token";

	private static IOptions<JwtOptions> JwtOptions() => Options.Create(options: new JwtOptions
	{
		Secret = "super-secret-test-key-at-least-32-chars!!",
		Issuer = "test",
		Audience = "test",
		AccessTokenTtlMinutes = 60,
		RefreshTokenTtlDays = 7
	});

	private static HttpContext ContextWithCookie(Stream body, string? refreshToken)
	{
		HttpContext context = HttpContextFactory.Create(body: body, method: "POST", path: "/api/v1/auth/refresh");

		if (refreshToken is not null)
			context.Request.Headers.Cookie = $"{CookieName}={refreshToken}";

		return context;
	}

	private static ISender SenderReturning(Result<SessionToken, AppException> result)
	{
		ISender sender = Substitute.For<ISender>();

		sender.Send(request: Arg.Any<RefreshTokenCommand>(), cancellationToken: Arg.Any<CancellationToken>())
			.Returns(returnThis: result);

		return sender;
	}

	private static SessionToken Session(string refreshToken) => new SessionToken(
		AccessToken: "access-token",
		RefreshToken: refreshToken,
		AccessTokenExpiresAt: DateTimeOffset.UtcNow.AddMinutes(minutes: 60),
		SessionId: Guid.CreateVersion7()
	);

	private static string? SetCookieHeader(HttpContext context)
		=> context.Response.Headers.SetCookie.FirstOrDefault(predicate: header => header?.StartsWith(value: CookieName, comparisonType: StringComparison.Ordinal) == true);

	[Test]
	public async Task HandleAsync_WithoutACookie_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = ContextWithCookie(body: body, refreshToken: null);

		ISender sender = Substitute.For<ISender>();

		await RefreshTokenEndpoint.HandleAsync(
			sender: sender,
			httpContext: context,
			jwtOptions: JwtOptions(),
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<RefreshTokenCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WithACookie_ShouldSendItsValue()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = ContextWithCookie(body: body, refreshToken: "the-old-token");

		ISender sender = SenderReturning(result: Result<SessionToken, AppException>.Success(value: Session(refreshToken: "the-new-token")));

		await RefreshTokenEndpoint.HandleAsync(
			sender: sender,
			httpContext: context,
			jwtOptions: JwtOptions(),
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<RefreshTokenCommand>(predicate: command => command!.RefreshToken == "the-old-token"),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_OnSuccess_ShouldReplaceTheCookie()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = ContextWithCookie(body: body, refreshToken: "the-old-token");

		ISender sender = SenderReturning(result: Result<SessionToken, AppException>.Success(value: Session(refreshToken: "the-new-token")));

		await RefreshTokenEndpoint.HandleAsync(
			sender: sender,
			httpContext: context,
			jwtOptions: JwtOptions(),
			ct: CancellationToken.None
		);

		string? cookie = SetCookieHeader(context: context);

		await Assert.That(value: cookie).IsNotNull();

		await Assert.That(value: cookie!).Contains(expected: "the-new-token").Because(message: """
			Rotation only means anything if the caller ends up holding the successor. Leaving the old one
			in place would let a stolen token be replayed indefinitely.
		""");
	}

	[Test]
	public async Task HandleAsync_OnFailure_ShouldClearTheCookie()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = ContextWithCookie(body: body, refreshToken: "a-revoked-token");

		ISender sender = SenderReturning(result: Result<SessionToken, AppException>.Failure(
			error: new InvalidTokenException(message: "Refresh token was revoked.")
		));

		await RefreshTokenEndpoint.HandleAsync(
			sender: sender,
			httpContext: context,
			jwtOptions: JwtOptions(),
			ct: CancellationToken.None
		);

		string? cookie = SetCookieHeader(context: context);

		await Assert.That(value: cookie).IsNotNull().Because(message: """
			Clearing a cookie is itself a Set-Cookie, with an expiry in the past. No header at all would
			mean the browser keeps presenting a token the server has already rejected.
		""");

		await Assert.That(value: cookie!).Contains(expected: "expires").IgnoringCase();
	}
}
