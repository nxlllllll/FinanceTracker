namespace FinanceTracker.Core.Services.Password;

public interface IPasswordHasher
{
	Task<string> Hash(string password);
	Task<bool> Verify(string password, string hash);
}