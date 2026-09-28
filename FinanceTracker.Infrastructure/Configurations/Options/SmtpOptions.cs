using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Configurations.Options;

public sealed class SmtpOptions
{
	public const string SectionName = "Smtp";

	[Required]
	public string Host { get; init; } = String.Empty;

	[Range(minimum: 1, maximum: 65_535)]
	public int Port { get; init; } = 587;

	[Required, EmailAddress]
	public string From { get; init; } = String.Empty;

	public string? UserName { get; init; }

	public string? Password { get; init; }
}
