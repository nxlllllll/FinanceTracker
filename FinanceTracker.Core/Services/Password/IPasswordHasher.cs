namespace FinanceTracker.Core.Services.Password;

/// <summary>
/// Abstracts password hashing and verification.
/// The underlying algorithm is Argon2id — resistant to brute-force and side-channel attacks.
/// </summary>
public interface IPasswordHasher
{
	/// <summary>Hashes a plaintext password. Returns a self-contained hash string including salt and parameters.</summary>
	Task<string> Hash(string password);

	/// <summary>Verifies a plaintext password against a previously computed hash.</summary>
	Task<bool> Verify(string password, string hash);
}