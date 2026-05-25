namespace FinanceTracker.Core.Services.Token;

public interface ITokenService
{
	AccessTokenResult GenerateAccessToken(Domains.User.User user);
	string GenerateRefreshToken();
	string HashRefreshToken(string refreshToken);
	DateTimeOffset GetRefreshTokenExpiry();
}
