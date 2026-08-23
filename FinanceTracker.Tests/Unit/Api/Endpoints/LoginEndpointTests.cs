using FinanceTracker.Api.Endpoints.Auth.Commands;
using FinanceTracker.Api.Endpoints.Auth.Contracts;
using FinanceTracker.Application.UseCases.User.Commands.LoginUser;
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

public sealed class LoginEndpointTests
{
	private const string ValidEmail = "user@test.com";
	private const string ValidPassword = "P@ssw0rd!";
	private const string CookieName = "__Host-refresh-token";

	private static IOptions<JwtOptions> JwtOptions() => Options.Create(options: new JwtOptions
	{
		Secret = "super-secret-test-key-at-least-32-chars!!",
		Issuer = "test",
		Audience = "test",
		AccessTokenTtlMinutes = 60,
		RefreshTokenTtlDays = 7
	});

	private static ISender SenderReturning(Result<SessionToken, AppException> result)
	{
		ISender sender = Substitute.For<ISender>();

		sender.Send(request: Arg.Any<LoginUserCommand>(), cancellationToken: Arg.Any<CancellationToken>())
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
	{
		return context.Response.Headers.SetCookie.FirstOrDefault(
			predicate: header => header?.StartsWith(value: CookieName, comparisonType: StringComparison.Ordinal) == true
		);
	}

	[Test]
	public async Task HandleAsync_WithAnInvalidEmail_ShouldNotReachTheHandler()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = HttpContextFactory.Create(body: body);

		ISender sender = Substitute.For<ISender>();

		await LoginEndpoint.HandleAsync(
			request: new LoginUserRequest(Email: "not-an-email", Password: ValidPassword),
			sender: sender,
			httpContext: context,
			jwtOptions: JwtOptions(),
			ct: CancellationToken.None
		);

		await sender.DidNotReceive().Send(request: Arg.Any<LoginUserCommand>(), cancellationToken: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WithAValidRequest_ShouldSendACommandBuiltFromIt()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = HttpContextFactory.Create(body: body);

		ISender sender = SenderReturning(result: Result<SessionToken, AppException>.Success(value: Session(refreshToken: "refresh")));

		await LoginEndpoint.HandleAsync(
			request: new LoginUserRequest(Email: ValidEmail, Password: ValidPassword),
			sender: sender,
			httpContext: context,
			jwtOptions: JwtOptions(),
			ct: CancellationToken.None
		);

		await sender.Received(requiredNumberOfCalls: 1).Send(
			request: Arg.Is<LoginUserCommand>(predicate: command =>
				command!.Email.Value == ValidEmail &&
				command.Password == ValidPassword
			),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_OnSuccess_ShouldSetTheRefreshTokenCookie()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = HttpContextFactory.Create(body: body);

		ISender sender = SenderReturning(result: Result<SessionToken, AppException>.Success(value: Session(refreshToken: "the-refresh-token")));

		await LoginEndpoint.HandleAsync(
			request: new LoginUserRequest(Email: ValidEmail, Password: ValidPassword),
			sender: sender,
			httpContext: context,
			jwtOptions: JwtOptions(),
			ct: CancellationToken.None
		);

		string? cookie = SetCookieHeader(context: context);

		await Assert.That(value: cookie).IsNotNull().Because(message: """
			Without the cookie the caller holds an access token and no way to renew it, so the session
			ends silently when that token expires.
		""");

		await Assert.That(value: cookie!).Contains(expected: "httponly").IgnoringCase().Because(message: """
			Readable by script means a cross-site scripting flaw hands over the long-lived credential
			rather than the short-lived one.
		""");

		await Assert.That(value: cookie).Contains(expected: "secure").IgnoringCase();

		await Assert.That(value: cookie).Contains(expected: "samesite=strict").IgnoringCase().Because(message: """
			A refresh token sent on a cross-site request is a refresh token an attacker's page can spend.
		""");
	}

	[Test]
	public async Task HandleAsync_OnSuccess_ShouldUseTheHostPrefixedCookieName()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = HttpContextFactory.Create(body: body);

		ISender sender = SenderReturning(result: Result<SessionToken, AppException>.Success(value: Session(refreshToken: "refresh")));

		await LoginEndpoint.HandleAsync(
			request: new LoginUserRequest(Email: ValidEmail, Password: ValidPassword),
			sender: sender,
			httpContext: context,
			jwtOptions: JwtOptions(),
			ct: CancellationToken.None
		);

		await Assert.That(value: SetCookieHeader(context: context)).IsNotNull();
	}

	[Test]
	public async Task HandleAsync_OnFailure_ShouldNotSetAnyCookie()
	{
		using MemoryStream body = new MemoryStream();
		HttpContext context = HttpContextFactory.Create(body: body);

		ISender sender = SenderReturning(result: Result<SessionToken, AppException>.Failure(
			error: new InvalidCredentialsException(message: "Invalid email or password.")
		));

		await LoginEndpoint.HandleAsync(
			request: new LoginUserRequest(Email: ValidEmail, Password: ValidPassword),
			sender: sender,
			httpContext: context,
			jwtOptions: JwtOptions(),
			ct: CancellationToken.None
		);

		await Assert.That(value: SetCookieHeader(context: context)).IsNull().Because(message: """
			A rejected login must leave the caller exactly as it found them. Issuing a cookie here would
			hand out a credential to someone who failed to prove they should have one.
		""");
	}
}
