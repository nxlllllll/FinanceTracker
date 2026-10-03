using FinanceTracker.Core.Services.Notification;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Services.Notification;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notification;

public sealed class NotificationTemplateRendererTests
{
	[Test]
	public async Task Render_ShouldFillEveryPlaceholderInSubjectAndBody()
	{
		NotificationMessage message = NotificationTemplateRenderer.Render(
			template: new NotificationTemplate { Subject = "{Category}: {Threshold}%", Body = "Spent {Spent} of {Limit} on {Category}." },
			values: new Dictionary<string, string> { ["Category"] = "Groceries", ["Threshold"] = "80", ["Spent"] = "8,500.00", ["Limit"] = "10,000.00" }
		);

		await Assert.That(value: message.Subject).IsEqualTo(expected: "Groceries: 80%");
		await Assert.That(value: message.Body).IsEqualTo(expected: "Spent 8,500.00 of 10,000.00 on Groceries.");
	}

	[Test]
	public async Task Render_WithAPlaceholderTheSenderDidNotSupply_ShouldThrow()
	{
		await Assert.That(action: () => NotificationTemplateRenderer.Render(
			template: new NotificationTemplate { Subject = "{Category}", Body = "{Missing}" },
			values: new Dictionary<string, string> { ["Category"] = "Groceries" }
		)).Throws<InvalidOperationException>().Because(message: "a notification reaching the user with literal {Missing} in it is worse than one that is retried and dead-lettered");
	}

	[Test]
	public async Task Render_ShouldIgnoreValuesTheTemplateDoesNotUse()
	{
		NotificationMessage message = NotificationTemplateRenderer.Render(
			template: new NotificationTemplate { Subject = "Plain subject", Body = "Plain body" },
			values: new Dictionary<string, string> { ["Unused"] = "value" }
		);

		await Assert.That(value: message.Subject).IsEqualTo(expected: "Plain subject");
	}
}
