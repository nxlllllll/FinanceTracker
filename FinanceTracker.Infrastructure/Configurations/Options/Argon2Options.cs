using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Configurations.Options;

public sealed class Argon2Options
{
	public const string SectionName = "Argon2";

	[Range(minimum: 19456, maximum: Int32.MaxValue)]
	public int MemorySize { get; init; } = 65536;

	[Range(minimum: 2, maximum: Int32.MaxValue)]
	public int Iterations { get; init; } = 3;

	[Range(minimum: 1, maximum: Int32.MaxValue)]
	public int DegreeOfParallelism { get; init; } = 4;

	[Range(minimum: 16, maximum: Int32.MaxValue)]
	public int HashLength { get; init; } = 32;

	[Range(minimum: 16, maximum: Int32.MaxValue)]
	public int SaltLength { get; init; } = 16;
}