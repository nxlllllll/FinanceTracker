using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Configurations.Options;

public sealed class NotificationOptionsValidator : IValidateOptions<NotificationOptions>
{
	public ValidateOptionsResult Validate(string? name, NotificationOptions options)
	{
		List<string> failures = [];

		if (options.Templates.Count == 0)
			failures.Add(item: $"{NotificationOptions.SectionName}:{nameof(NotificationOptions.Templates)} lists no templates.");

		foreach ((string key, NotificationTemplate template) in options.Templates)
		{
			if (String.IsNullOrWhiteSpace(value: template.Subject))
				failures.Add(item: $"Notification template '{key}' has no subject.");

			if (String.IsNullOrWhiteSpace(value: template.Body))
				failures.Add(item: $"Notification template '{key}' has no body.");
		}

		return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures: failures);
	}
}
