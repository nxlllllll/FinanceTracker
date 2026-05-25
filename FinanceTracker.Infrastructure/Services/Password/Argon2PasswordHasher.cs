using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Infrastructure.Configurations.Options;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Services.Password;

public sealed class Argon2PasswordHasher(
	IOptions<Argon2Options> options
) : IPasswordHasher
{
	private readonly Argon2Options _options = options.Value;

	public async Task<string> Hash(string password)
	{
		byte[] salt = RandomNumberGenerator.GetBytes(count: _options.SaltLength);
		byte[] hash = await ComputeHash(password: password, salt: salt);

		return $"{Convert.ToBase64String(inArray: salt)}:{Convert.ToBase64String(inArray: hash)}";
	}

	public async Task<bool> Verify(string password, string storedHash)
	{
		string[] parts = storedHash.Split(separator: ':');

		if (parts.Length != 2)
			return false;

		byte[] salt;
		byte[] expectedHash;

		try
		{
			salt = Convert.FromBase64String(s: parts[0]);
			expectedHash = Convert.FromBase64String(s: parts[1]);
		}
		catch (FormatException)
		{
			return false;
		}

		byte[] actualHash = await ComputeHash(password: password, salt: salt);

		return CryptographicOperations.FixedTimeEquals(left: actualHash, right: expectedHash);
	}

	private async Task<byte[]> ComputeHash(string password, byte[] salt)
	{
		using Argon2id argon2 = new Argon2id(password: Encoding.UTF8.GetBytes(s: password));

		argon2.Salt = salt;
		argon2.MemorySize = _options.MemorySize;
		argon2.Iterations = _options.Iterations;
		argon2.DegreeOfParallelism = _options.DegreeOfParallelism;

		return await argon2.GetBytesAsync(bc: _options.HashLength);
	}
}
