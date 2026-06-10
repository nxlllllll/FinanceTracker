using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Services.Password;

/// <summary>
/// Configuration for the Argon2id password hashing algorithm.
/// Bind from <c>appsettings.json</c> under the <c>"Argon2"</c> section.
/// Default values meet OWASP minimum recommendations for Argon2id.
/// </summary>
public sealed class Argon2Options
{
	public const string SectionName = "Argon2";

	/// <summary>Memory usage in KB. OWASP minimum: 19456 KB (19 MB). Default: 65536 KB (64 MB).</summary>
	[Range(minimum: 19456, maximum: Int32.MaxValue)]
	public int MemorySize { get; init; } = 65536;

	/// <summary>Number of hashing iterations. OWASP minimum: 2. Default: 3.</summary>
	[Range(minimum: 2, maximum: Int32.MaxValue)]
	public int Iterations { get; init; } = 3;

	/// <summary>Degree of parallelism (number of threads). Default: 4.</summary>
	[Range(minimum: 1, maximum: Int32.MaxValue)]
	public int DegreeOfParallelism { get; init; } = 4;

	/// <summary>Output hash length in bytes. Default: 32.</summary>
	[Range(minimum: 16, maximum: Int32.MaxValue)]
	public int HashLength { get; init; } = 32;

	/// <summary>Random salt length in bytes. Default: 16.</summary>
	[Range(minimum: 16, maximum: Int32.MaxValue)]
	public int SaltLength { get; init; } = 16;
}