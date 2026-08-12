using System.Security.Claims;
using System.Text;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Infrastructure.Services.Token;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinanceTracker.Api.Security;

/// <summary>
/// Configures JWT validation as the exact mirror of token generation in
/// <c>JwtTokenService</c>: same HMAC-SHA256 key, issuer and audience.
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

	private async Task OnTokenValidatedAsync(TokenValidatedContext context)
	{
		string? sidClaim = context.Principal?.FindFirstValue(claimType: JwtRegisteredClaimNames.Sid);
		if (sidClaim is null || !Guid.TryParse(input: sidClaim, result: out Guid sessionId))
		{
			context.Fail(failureMessage: "Token is missing a valid session id.");
			return;
		}

		ISessionValidator sessionValidator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();

		bool isActive = await sessionValidator.IsSessionActiveAsync(
			sessionId: sessionId,
			ct: context.HttpContext.RequestAborted
		);

		if (!isActive)
			context.Fail(failureMessage: "Session is no longer active.");
	}
}
