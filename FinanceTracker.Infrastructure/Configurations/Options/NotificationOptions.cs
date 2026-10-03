namespace FinanceTracker.Infrastructure.Configurations.Options;

public sealed class NotificationOptions
{
	public const string SectionName = "Notifications";

	public Dictionary<string, NotificationTemplate> Templates { get; init; } = [];
}

public sealed class NotificationTemplate
{
	public string Subject { get; init; } = String.Empty;

	public string Body { get; init; } = String.Empty;
}
