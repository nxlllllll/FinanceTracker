using System.Text.RegularExpressions;
using FinanceTracker.Core.Services.Notification;
using FinanceTracker.Infrastructure.Configurations.Options;

namespace FinanceTracker.Infrastructure.Services.Notification;

public static partial class NotificationTemplateRenderer
{
	[GeneratedRegex(pattern: @"\{(?<name>\w+)\}")]
	private static partial Regex Placeholder();

	public static NotificationMessage Render(
		NotificationTemplate template,
		IReadOnlyDictionary<string, string> values
	) => new NotificationMessage(
		Subject: FillPlaceholders(text: template.Subject, values: values),
		Body: FillPlaceholders(text: template.Body, values: values)
	);

	private static string FillPlaceholders(
		string text,
		IReadOnlyDictionary<string, string> values)
	{
		return Placeholder().Replace(
			input: text,
			evaluator: placeholder => GetValue(placeholder: placeholder, values: values)
		);
	}

	private static string GetValue(
		Match placeholder,
		IReadOnlyDictionary<string, string> values)
	{
		if (values.TryGetValue(key: placeholder.Groups["name"].Value, value: out string? value))
			return value;

		throw new InvalidOperationException(
			message: $"Notification template uses '{placeholder.Value}', which the sender did not supply."
		);
	}
}
