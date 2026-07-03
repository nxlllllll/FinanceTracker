using System.Security.Cryptography;
using System.Text;

namespace FinanceTracker.Core.Utilities;

/// <summary>
/// Generates deterministic GUIDs from a set of inputs using SHA-256.
/// Used to produce stable message IDs for idempotency — the same inputs
/// always produce the same GUID, preventing duplicate processing on retry.
/// </summary>
public static class DeterministicGuid
{
	/// <summary>
	/// Produces a deterministic GUID from a base ID, year, and month.
	/// Used by <c>RecurringTransactionHandlingJob</c> to ensure each
	/// recurring transaction fires at most once per calendar month.
	/// </summary>
	public static Guid Create(Guid baseId, int year, int month)
	{
		string input = $"{baseId}:{year}:{month}";
		byte[] inputBytes = Encoding.UTF8.GetBytes(s: input);
		byte[] hashBytes = SHA256.HashData(source: inputBytes);
		return new Guid(b: hashBytes[..16]);
	}
}
