using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Blake3;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using FinanceTracker.Infrastructure.Configurations.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Services.Token;

public sealed class JwtTokenService(
	IOptions<JwtOptions> options,
	IDateProvider dateProvider
) : ITokenService
{
	private readonly JwtOptions _options = options.Value;

	public AccessTokenResult GenerateAccessToken(Core.Domains.User.User user)
	{
		DateTimeOffset expiresAt = dateProvider.UtcNow.AddMinutes(minutes: _options.AccessTokenTtlMinutes);

		List<Claim> claims =
		[
			new Claim(type: JwtRegisteredClaimNames.Sub, value: user.Id.ToString()),
			new Claim(type: JwtRegisteredClaimNames.Email, value: user.Email.Value),
			new Claim(type: JwtRegisteredClaimNames.Jti, value: Guid.CreateVersion7().ToString())
		];

		SymmetricSecurityKey key = new SymmetricSecurityKey(key: Encoding.UTF8.GetBytes(s: _options.Secret));
		SigningCredentials credentials = new SigningCredentials(key: key, algorithm: SecurityAlgorithms.HmacSha256);

		JwtSecurityToken token = new JwtSecurityToken(
			issuer: _options.Issuer,
			audience: _options.Audience,
			claims: claims,
			expires: expiresAt.UtcDateTime,
			signingCredentials: credentials
		);

		return new AccessTokenResult(
			Token: new JwtSecurityTokenHandler().WriteToken(token: token),
			ExpiresAt: expiresAt
		);
	}

	public string GenerateRefreshToken()
		=> Convert.ToBase64String(inArray: RandomNumberGenerator.GetBytes(count: 32));

	public string HashRefreshToken(string refreshToken)
	{
		Hash hash = Hasher.Hash(input: Encoding.UTF8.GetBytes(s: refreshToken));
		return Convert.ToHexString(inArray: hash.AsSpan().ToArray()).ToLowerInvariant();
	}

	public DateTimeOffset GetRefreshTokenExpiry()
		=> dateProvider.UtcNow.AddDays(days: _options.RefreshTokenTtlDays);
}
