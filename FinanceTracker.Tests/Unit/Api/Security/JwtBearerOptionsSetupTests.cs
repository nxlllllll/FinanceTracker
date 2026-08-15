using System.Security.Claims;
using System.Text;
using FinanceTracker.Api.Security;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Infrastructure.Services.Token;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Api.Security;

public sealed class JwtBearerOptionsSetupTests
{
	private const string Secret = "a-signing-secret-long-enough-for-hmac-sha256";

	private static readonly JwtOptions Jwt = new JwtOptions
	{
		Secret = Secret,
		Issuer = "FinanceTracker",
		Audience = "FinanceTracker"
	};

	private static JwtBearerOptions ConfiguredOptions(string? scheme = JwtBearerDefaults.AuthenticationScheme)
	{
		JwtBearerOptions options = new JwtBearerOptions();

		new JwtBearerOptionsSetup(jwtOptions: Options.Create(options: Jwt)).Configure(name: scheme, options: options);

		return options;
	}

	[Test]
	public async Task ValidationAcceptsOnlyTokensThisApiIssued()
	{
		TokenValidationParameters parameters = ConfiguredOptions().TokenValidationParameters;

		await Assert.That(value: parameters.ValidateIssuer).IsTrue();
		await Assert.That(value: parameters.ValidIssuer).IsEqualTo(expected: Jwt.Issuer);
		await Assert.That(value: parameters.ValidateAudience).IsTrue();
		await Assert.That(value: parameters.ValidAudience).IsEqualTo(expected: Jwt.Audience);
		await Assert.That(value: parameters.ValidateLifetime).IsTrue();
		await Assert.That(value: parameters.ValidateIssuerSigningKey).IsTrue();
	}

	[Test]
	public async Task ValidationUsesTheSameKeyTheTokensAreSignedWith()
	{
		SymmetricSecurityKey key = (SymmetricSecurityKey)ConfiguredOptions().TokenValidationParameters.IssuerSigningKey;

		await Assert.That(value: key.Key).IsEquivalentTo(expected: Encoding.UTF8.GetBytes(s: Secret))
			.Because(message: "a key that differs from the signing one rejects every token the API itself issued");
	}

	[Test]
	public async Task ClockSkewIsTightRatherThanTheFiveMinuteDefault()
	{
		await Assert.That(value: ConfiguredOptions().TokenValidationParameters.ClockSkew)
			.IsEqualTo(expected: TimeSpan.FromSeconds(value: 30))
			.Because(message: "the default five minutes would keep a revoked token usable long after its stated expiry");
	}

	[Test]
	public async Task ClaimNamesArriveAsTheyWereWritten()
	{
		await Assert.That(value: ConfiguredOptions().MapInboundClaims).IsFalse()
			.Because(message: "remapping would rename sid and sub, and the session lookup reads them by their JWT names");
	}

	[Test]
	public async Task AnotherSchemeIsLeftAlone()
	{
		JwtBearerOptions options = ConfiguredOptions(scheme: "SomeOtherScheme");

		await Assert.That(value: options.MapInboundClaims).IsTrue()
			.Because(message: "configuring by name means only the named scheme may be touched");
	}

	private static async Task<TokenValidatedContext> ValidateAsync(ClaimsPrincipal principal, bool sessionIsActive)
	{
		ISessionValidator sessionValidator = Substitute.For<ISessionValidator>();
		sessionValidator.IsSessionActiveAsync(sessionId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: sessionIsActive);

		DefaultHttpContext httpContext = new DefaultHttpContext
		{
			RequestServices = new ServiceCollection().AddSingleton(implementationInstance: sessionValidator).BuildServiceProvider()
		};

		JwtBearerOptions options = ConfiguredOptions();

		TokenValidatedContext context = new TokenValidatedContext(
			context: httpContext,
			scheme: new AuthenticationScheme(
				name: JwtBearerDefaults.AuthenticationScheme,
				displayName: null,
				handlerType: typeof(JwtBearerHandler)
			),
			options: options
		)
		{
			Principal = principal
		};

		await options.Events.OnTokenValidated(context);

		return context;
	}

	private static ClaimsPrincipal PrincipalWith(params Claim[] claims)
		=> new ClaimsPrincipal(identity: new ClaimsIdentity(claims: claims, authenticationType: "Test"));

	[Test]
	public async Task ATokenWithoutASessionIdIsRejected()
	{
		TokenValidatedContext context = await ValidateAsync(
			principal: PrincipalWith(new Claim(type: JwtRegisteredClaimNames.Sub, value: Guid.CreateVersion7().ToString())),
			sessionIsActive: true
		);

		await Assert.That(value: context.Result?.Succeeded).IsFalse()
			.Because(message: "a token carrying no session cannot be checked for revocation, so it cannot be trusted");
	}

	[Test]
	public async Task ATokenWhoseSessionIdIsNotAGuidIsRejected()
	{
		TokenValidatedContext context = await ValidateAsync(
			principal: PrincipalWith(new Claim(type: JwtRegisteredClaimNames.Sid, value: "not-a-guid")),
			sessionIsActive: true
		);

		await Assert.That(value: context.Result?.Succeeded).IsFalse();
	}

	[Test]
	public async Task ATokenFromAClosedSessionIsRejectedEvenThoughItsSignatureIsValid()
	{
		TokenValidatedContext context = await ValidateAsync(
			principal: PrincipalWith(new Claim(type: JwtRegisteredClaimNames.Sid, value: Guid.CreateVersion7().ToString())),
			sessionIsActive: false
		);

		await Assert.That(value: context.Result?.Succeeded).IsFalse();
	}

	[Test]
	public async Task ATokenFromAnOpenSessionPassesThrough()
	{
		TokenValidatedContext context = await ValidateAsync(
			principal: PrincipalWith(new Claim(type: JwtRegisteredClaimNames.Sid, value: Guid.CreateVersion7().ToString())),
			sessionIsActive: true
		);

		await Assert.That(value: context.Result).IsNull()
			.Because(message: "leaving the result untouched is how the handler signals it has no objection");
	}
}
