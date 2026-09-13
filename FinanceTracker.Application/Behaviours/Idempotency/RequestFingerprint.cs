using System.Security.Cryptography;
using System.Text.Json;
using FinanceTracker.Core.Converters.Json;

namespace FinanceTracker.Application.Behaviours.Idempotency;

public static class RequestFingerprint
{
	public static string GetHash(object fingerprint)
	{
		byte[] json = JsonSerializer.SerializeToUtf8Bytes(
			value: fingerprint,
			inputType: fingerprint.GetType(),
			options: FinanceTrackerJsonOptions.Payload
		);
		return Convert.ToHexStringLower(bytes: SHA256.HashData(source: json));
	}
}
