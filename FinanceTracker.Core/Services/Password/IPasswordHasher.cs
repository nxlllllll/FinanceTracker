namespace FinanceTracker.Core.Services.Password;

/// <summary>
/// Abstracts password hashing and verification.
/// The underlying algorithm is Argon2id — resistant to brute-force and side-channel attacks.
/// </summary>
public interface IPasswordHasher
{
	/// <summary>Hashes a plaintext password. Returns a self-contained hash string including salt and parameters.</summary>
	Task<string> Hash(string password);

	/// <summary>
	/// Verifies a plaintext password against a previously computed hash.
	/// </summary>
	/// <param name="password">The plaintext password supplied by the caller.</param>
	/// <param name="storedHash">
	/// The hash to verify against, or <c>null</c> when no account/hash exists for the supplied identifier
	/// (e.g. login attempt for an email that isn't registered). Passing <c>null</c> still performs a full,
	/// equally expensive verification against a fixed dummy hash, so that callers never need to special-case
	/// "account not found" to avoid a timing side-channel that would let an attacker distinguish
	/// "no such account" from "wrong password" by measuring response time.
	/// </param>
	Task<bool> Verify(string password, string? storedHash);
}