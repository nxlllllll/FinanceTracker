using System.Security.Cryptography;
using System.Text;

namespace FinanceTracker.Core.Utilities;

public static class DeterministicGuid
{
	public static Guid Create(Guid baseId, int year, int month)
	{
		string input = $"{baseId}:{year}:{month}";
		byte[] inputBytes = Encoding.UTF8.GetBytes(s: input);
		byte[] hashBytes = SHA256.HashData(source: inputBytes);
		return new Guid(b: hashBytes[..16]);
	}
}