using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FinanceTracker.Core.Services.Password;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Services.Password;

public sealed partial class Argon2PasswordHasher(
	IOptionsMonitor<Argon2Options> options
) : IPasswordHasher
{
	/// <summary>
	/// Fixed salt used only for the "no such account" verification path (see <see cref="Verify"/>).
	/// </summary>
	private static readonly byte[] DummySalt = Convert.FromBase64String(s: "SQzm0/WXdnO0CHsghWSVIQ==");

	private const int MemorySizeMultiplier = 2;
	private const int IterationsMultiplier = 2;
	private const int ParallelismMultiplier = 4;

	[GeneratedRegex(pattern: @"^\$argon2id\$v=(?<version>\d+)\$m=(?<memory>\d+),t=(?<iterations>\d+),p=(?<parallelism>\d+)\$(?<salt>[A-Za-z0-9+/=]+)\$(?<hash>[A-Za-z0-9+/=]+)$", options: RegexOptions.Compiled)]
	private static partial Regex PhcFormatRegex();

	/// <summary>
	/// Hashes <paramref name="password"/> and returns a self-describing PHC-format string
	/// (<c>$argon2id$v=19$m=...,t=...,p=...$salt$hash</c>). The parameters used at hash time
	/// are embedded in the result, so <see cref="Verify"/> never depends on the current
	/// <see cref="Argon2Options"/> — changing the config only affects newly created hashes.
	/// </summary>
	public async Task<string> Hash(string password)
	{
		Argon2Options current = options.CurrentValue;

		byte[] salt = RandomNumberGenerator.GetBytes(count: current.SaltLength);
		byte[] hash = await ComputeHash(
			password: password,
			salt: salt,
			memorySize: current.MemorySize,
			iterations: current.Iterations,
			degreeOfParallelism: current.DegreeOfParallelism,
			hashLength: current.HashLength
		);

		return $"$argon2id$v=19$m={current.MemorySize},t={current.Iterations},p={current.DegreeOfParallelism}${Convert.ToBase64String(inArray: salt)}${Convert.ToBase64String(inArray: hash)}";
	}

	/// <summary>
	/// Verifies <paramref name="password"/> against <paramref name="storedHash"/> using the
	/// parameters embedded in the stored hash itself, not the current <see cref="Argon2Options"/>.
	/// This is what makes hashes durable across config changes: an older hash created with weaker
	/// parameters still verifies correctly even after <see cref="Argon2Options"/> is strengthened.
	/// </summary>
	public async Task<bool> Verify(string password, string? storedHash)
	{
		Argon2Options current = options.CurrentValue;

		if (storedHash is null)
		{
			await ComputeHash(
				password: password,
				salt: DummySalt,
				memorySize: current.MemorySize,
				iterations: current.Iterations,
				degreeOfParallelism: current.DegreeOfParallelism,
				hashLength: current.HashLength
			);
			return false;
		}

		Match match = PhcFormatRegex().Match(input: storedHash);
		if (!match.Success)
			return false;

		if (!Int32.TryParse(s: match.Groups["memory"].Value, result: out int memorySize) ||
			!Int32.TryParse(s: match.Groups["iterations"].Value, result: out int iterations) ||
			!Int32.TryParse(s: match.Groups["parallelism"].Value, result: out int parallelism)
		) return false;

		if (memorySize <= 0 || memorySize > current.MemorySize * MemorySizeMultiplier ||
			iterations <= 0 || iterations > current.Iterations * IterationsMultiplier ||
			parallelism <= 0 || parallelism > current.DegreeOfParallelism * ParallelismMultiplier
		) return false;

		byte[] salt;
		byte[] expectedHash;

		try
		{
			salt = Convert.FromBase64String(s: match.Groups["salt"].Value);
			expectedHash = Convert.FromBase64String(s: match.Groups["hash"].Value);
		}
		catch (FormatException)
		{
			return false;
		}

		byte[] actualHash = await ComputeHash(
			password: password,
			salt: salt,
			memorySize: memorySize,
			iterations: iterations,
			degreeOfParallelism: parallelism,
			hashLength: expectedHash.Length
		);

		return CryptographicOperations.FixedTimeEquals(left: actualHash, right: expectedHash);
	}

	private async Task<byte[]> ComputeHash(
		string password,
		byte[] salt,
		int memorySize,
		int iterations,
		int degreeOfParallelism,
		int hashLength)
	{
		using Argon2id argon2 = new Argon2id(password: Encoding.UTF8.GetBytes(s: password));

		argon2.Salt = salt;
		argon2.MemorySize = memorySize;
		argon2.Iterations = iterations;
		argon2.DegreeOfParallelism = degreeOfParallelism;

		return await argon2.GetBytesAsync(bc: hashLength);
	}
}
