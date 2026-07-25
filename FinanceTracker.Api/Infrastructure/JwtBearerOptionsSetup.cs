using System.Security.Claims;
using System.Text;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Services.Token;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinanceTracker.Api.Infrastructure;

/// <summary>
/// Configures JWT validation as the exact mirror of token generation in
/// <c>JwtTokenService</c>: same HMAC-SHA256 key, issuer and audience.
/// <para>
/// Also closes the gap between "session revoked in the database" and "access token stops
/// working": standard signature/lifetime validation only proves the token was genuinely issued
/// and hasn't expired yet — it says nothing about whether the session behind it was revoked in
/// the meantime. <see cref="OnTokenValidated"/> adds one cheap Redis lookup per request to reject
/// a token whose session was revoked, without needing a database round trip. If Redis is
/// unreachable, <c>RedisCache</c> already fails open (reports "not revoked") — the worst case is
/// reverting to today's signature/lifetime-only behavior, not a new outage.
/// </para>
/// </summary>
public sealed class JwtBearerOptionsSetup(
	IOptions<JwtOptions> jwtOptions
) : IConfigureNamedOptions<JwtBearerOptions>
{
	public void Configure(string? name, JwtBearerOptions options)
	{
		if (name != JwtBearerDefaults.AuthenticationScheme)
			return;

		JwtOptions jwt = jwtOptions.Value;

		options.MapInboundClaims = false;

		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = jwt.Issuer,
			ValidateAudience = true,
			ValidAudience = jwt.Audience,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(key: Encoding.UTF8.GetBytes(s: jwt.Secret)),
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromSeconds(value: 30)
		};

		options.Events = new JwtBearerEvents
		{
			OnTokenValidated = OnTokenValidatedAsync
		};
	}

	public void Configure(JwtBearerOptions options) => Configure(
		name: JwtBearerDefaults.AuthenticationScheme,
		options: options
	);

	private static async Task OnTokenValidatedAsync(TokenValidatedContext context)
	{
		string? sidClaim = context.Principal?.FindFirstValue(claimType: JwtRegisteredClaimNames.Sid);
		if (sidClaim is null || !Guid.TryParse(input: sidClaim, result: out Guid sessionId))
		{
			context.Fail(failureMessage: "Token is missing a valid session id.");
			return;
		}

		RedisCache redisCache = context.HttpContext.RequestServices.GetRequiredService<RedisCache>();

		CacheEntry<bool> entry = await redisCache.TryGetAsync<bool>(key: SessionRevocationCacheKeys.RevokedSessionKey(sessionId: sessionId));
		if (entry is { Found: true, Value: true })
			context.Fail(failureMessage: "Session has been revoked.");
	}
}
