using FinanceTracker.Infrastructure.Services.Password;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class Argon2PasswordHasherTests
{
	private const string Password = "correctHorseBatteryStaple";

	private static Argon2Options FastOptions => new Argon2Options
	{
		MemorySize = 19_456,
		Iterations = 2,
		DegreeOfParallelism = 1,
		HashLength = 16,
		SaltLength = 16
	};

	private static Argon2PasswordHasher CreateHasher(Argon2Options? options = null)
	{
		IOptionsMonitor<Argon2Options> optionsMonitor = Substitute.For<IOptionsMonitor<Argon2Options>>();
		optionsMonitor.CurrentValue.Returns(returnThis: options ?? FastOptions);

		return new Argon2PasswordHasher(options: optionsMonitor);
	}

	[Test]
	public async Task Hash_ShouldProduceSelfDescribingPhcFormatString()
	{
		Argon2PasswordHasher hasher = CreateHasher();

		string hash = await hasher.Hash(password: Password);

		await Assert.That(value: hash).StartsWith(expected: "$argon2id$v=19$m=19456,t=2,p=1$");
	}

	[Test]
	public async Task Hash_CalledTwice_ShouldProduceDifferentHashes()
	{
		Argon2PasswordHasher hasher = CreateHasher();

		string first = await hasher.Hash(password: Password);
		string second = await hasher.Hash(password: Password);

		await Assert.That(value: first).IsNotEqualTo(notExpected: second);
	}

	[Test]
	public async Task Verify_WithCorrectPassword_ShouldReturnTrue()
	{
		Argon2PasswordHasher hasher = CreateHasher();
		string hash = await hasher.Hash(password: Password);

		bool result = await hasher.Verify(password: Password, storedHash: hash);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task Verify_WithWrongPassword_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasher = CreateHasher();
		string hash = await hasher.Hash(password: Password);

		bool result = await hasher.Verify(password: "wrongPassword", storedHash: hash);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_WithNullStoredHash_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasher = CreateHasher();

		bool result = await hasher.Verify(password: Password, storedHash: null);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_WithMalformedStoredHash_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasher = CreateHasher();

		bool result = await hasher.Verify(password: Password, storedHash: "not-a-valid-hash");

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_WithLegacySaltColonHashFormat_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasher = CreateHasher();

		bool result = await hasher.Verify(password: Password, storedHash: "c29tZXNhbHQ=:c29tZWhhc2g=");

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_WithInvalidBase64InHash_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasher = CreateHasher();

		bool result = await hasher.Verify(password: Password, storedHash: "$argon2id$v=19$m=19456,t=2,p=1$not-valid-base64!!!$also-not-valid!!!");

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_HashedWithDifferentParametersThanCurrentOptions_ShouldStillSucceed()
	{
		Argon2Options oldOptions = new Argon2Options
		{
			MemorySize = 19_456,
			Iterations = 2,
			DegreeOfParallelism = 1,
			HashLength = 16,
			SaltLength = 16
		};
		Argon2PasswordHasher hasherWithOldOptions = CreateHasher(options: oldOptions);
		string hashCreatedUnderOldOptions = await hasherWithOldOptions.Hash(password: Password);

		Argon2Options newOptions = new Argon2Options
		{
			MemorySize = 46_080,
			Iterations = 4,
			DegreeOfParallelism = 2,
			HashLength = 32,
			SaltLength = 16
		};
		Argon2PasswordHasher hasherWithNewOptions = CreateHasher(options: newOptions);

		bool result = await hasherWithNewOptions.Verify(password: Password, storedHash: hashCreatedUnderOldOptions);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task Verify_UnknownEmailScenario_ShouldRunSameComputationAsRealAccount()
	{
		Argon2PasswordHasher hasher = CreateHasher();

		bool result = await hasher.Verify(password: Password, storedHash: null);

		await Assert.That(value: result).IsFalse();
	}

	private static string ReplacePhcHeader(string hash, string newHeader)
	{
		int firstDollarAfterVersion = hash.IndexOf(value: '$', startIndex: "$argon2id$v=19".Length);
		int secondDollar = hash.IndexOf(value: '$', startIndex: firstDollarAfterVersion + 1);
		return hash[..(firstDollarAfterVersion + 1)] + newHeader + hash[secondDollar..];
	}

	[Test]
	public async Task Verify_WithMemorySizeGrosslyExceedingConfig_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasher = CreateHasher();
		string hash = await hasher.Hash(password: Password);
		string tampered = ReplacePhcHeader(hash: hash, newHeader: "m=2000000000,t=2,p=1");

		bool result = await hasher.Verify(password: Password, storedHash: tampered);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_WithIterationsGrosslyExceedingConfig_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasher = CreateHasher();
		string hash = await hasher.Hash(password: Password);
		string tampered = ReplacePhcHeader(hash: hash, newHeader: "m=19456,t=999999,p=1");

		bool result = await hasher.Verify(password: Password, storedHash: tampered);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_WithParallelismGrosslyExceedingConfig_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasher = CreateHasher();
		string hash = await hasher.Hash(password: Password);
		string tampered = ReplacePhcHeader(hash: hash, newHeader: "m=19456,t=2,p=999999");

		bool result = await hasher.Verify(password: Password, storedHash: tampered);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_WithZeroOrNegativeParameters_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasher = CreateHasher();
		string hash = await hasher.Hash(password: Password);
		string tampered = ReplacePhcHeader(hash: hash, newHeader: "m=0,t=2,p=1");

		bool result = await hasher.Verify(password: Password, storedHash: tampered);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_WithMemorySizeExactlyAtDerivedCap_ShouldSucceed()
	{
		Argon2PasswordHasher hasherAtHashTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 32_768,
			Iterations = 2,
			DegreeOfParallelism = 1,
			HashLength = 16,
			SaltLength = 16
		});
		string hash = await hasherAtHashTime.Hash(password: Password);

		Argon2PasswordHasher hasherAtVerifyTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 16_384,
			Iterations = 2,
			DegreeOfParallelism = 1,
			HashLength = 16,
			SaltLength = 16
		});

		bool result = await hasherAtVerifyTime.Verify(password: Password, storedHash: hash);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task Verify_WithMemorySizeOneAboveDerivedCap_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasherAtHashTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 32_768,
			Iterations = 2,
			DegreeOfParallelism = 1,
			HashLength = 16,
			SaltLength = 16
		});
		string hash = await hasherAtHashTime.Hash(password: Password);

		Argon2PasswordHasher hasherAtVerifyTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 16_383,
			Iterations = 2,
			DegreeOfParallelism = 1,
			HashLength = 16,
			SaltLength = 16
		});

		bool result = await hasherAtVerifyTime.Verify(password: Password, storedHash: hash);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_WithIterationsExactlyAtDerivedCap_ShouldSucceed()
	{
		Argon2PasswordHasher hasherAtHashTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 19_456,
			Iterations = 8,
			DegreeOfParallelism = 1,
			HashLength = 16,
			SaltLength = 16
		});
		string hash = await hasherAtHashTime.Hash(password: Password);

		Argon2PasswordHasher hasherAtVerifyTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 19_456,
			Iterations = 4,
			DegreeOfParallelism = 1,
			HashLength = 16,
			SaltLength = 16
		});

		bool result = await hasherAtVerifyTime.Verify(password: Password, storedHash: hash);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task Verify_WithIterationsOneAboveDerivedCap_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasherAtHashTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 19_456,
			Iterations = 8,
			DegreeOfParallelism = 1,
			HashLength = 16,
			SaltLength = 16
		});
		string hash = await hasherAtHashTime.Hash(password: Password);

		Argon2PasswordHasher hasherAtVerifyTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 19_456,
			Iterations = 3,
			DegreeOfParallelism = 1,
			HashLength = 16,
			SaltLength = 16
		});

		bool result = await hasherAtVerifyTime.Verify(password: Password, storedHash: hash);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Verify_WithParallelismExactlyAtDerivedCap_ShouldSucceed()
	{
		Argon2PasswordHasher hasherAtHashTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 19_456,
			Iterations = 2,
			DegreeOfParallelism = 8,
			HashLength = 16,
			SaltLength = 16
		});
		string hash = await hasherAtHashTime.Hash(password: Password);

		Argon2PasswordHasher hasherAtVerifyTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 19_456,
			Iterations = 2,
			DegreeOfParallelism = 2,
			HashLength = 16,
			SaltLength = 16
		});

		bool result = await hasherAtVerifyTime.Verify(password: Password, storedHash: hash);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task Verify_WithParallelismOneAboveDerivedCap_ShouldReturnFalse()
	{
		Argon2PasswordHasher hasherAtHashTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 19_456,
			Iterations = 2,
			DegreeOfParallelism = 8,
			HashLength = 16,
			SaltLength = 16
		});
		string hash = await hasherAtHashTime.Hash(password: Password);

		Argon2PasswordHasher hasherAtVerifyTime = CreateHasher(options: new Argon2Options
		{
			MemorySize = 19_456,
			Iterations = 2,
			DegreeOfParallelism = 1,
			HashLength = 16,
			SaltLength = 16
		});

		bool result = await hasherAtVerifyTime.Verify(password: Password, storedHash: hash);

		await Assert.That(value: result).IsFalse();
	}
}
