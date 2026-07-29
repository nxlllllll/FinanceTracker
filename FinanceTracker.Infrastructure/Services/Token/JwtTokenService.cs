using System.Security.Cryptography;
using System.Text;
using Blake3;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinanceTracker.Infrastructure.Services.Token;

/// <summary>
/// Generates signed JWT access tokens and opaque refresh tokens.
/// Access tokens are signed with HMAC-SHA256 using <see cref="JwtOptions.Secret"/>.
/// Refresh tokens are random 32-byte values hashed with Blake3 before storage.
/// </summary>
public sealed class JwtTokenService(
	IOptions<JwtOptions> options,
	IDateProvider dateProvider
) : ITokenService
{
	private readonly JwtOptions _options = options.Value;

	private static readonly JsonWebTokenHandler Handler = new JsonWebTokenHandler();

	public AccessTokenResult GenerateAccessToken(Core.Domains.User.User user, Guid sessionId)
	{
		DateTimeOffset expiresAt = dateProvider.UtcNow.AddMinutes(minutes: _options.AccessTokenTtlMinutes);

		SymmetricSecurityKey key = new SymmetricSecurityKey(key: Encoding.UTF8.GetBytes(s: _options.Secret));
		SigningCredentials credentials = new SigningCredentials(key: key, algorithm: SecurityAlgorithms.HmacSha256);

		SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
		{
			Issuer = _options.Issuer,
			Audience = _options.Audience,
			Expires = expiresAt.UtcDateTime,
			SigningCredentials = credentials,
			Claims = new Dictionary<string, object>
			{
				[JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
				[JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
				[JwtRegisteredClaimNames.Sid] = sessionId.ToString()
			}
		};

		return new AccessTokenResult(
			Token: Handler.CreateToken(tokenDescriptor: descriptor),
			ExpiresAt: expiresAt
		);
	}

	public string GenerateRefreshToken()
		=> Convert.ToBase64String(inArray: RandomNumberGenerator.GetBytes(count: 32));

	public string HashRefreshToken(string refreshToken)
	{
		Hash hash = Hasher.Hash(input: Encoding.UTF8.GetBytes(s: refreshToken));
		return Convert.ToHexStringLower(bytes: hash.AsSpan());
	}

	public DateTimeOffset GetRefreshTokenExpiry()
		=> dateProvider.UtcNow.AddDays(days: _options.RefreshTokenTtlDays);
}
