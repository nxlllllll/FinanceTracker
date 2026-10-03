using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FinanceTracker.Core.Services.Notification;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Services.Notification;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Services.Notification;

public sealed class EmailNotificationSenderTests
{
	private const int SmtpPort = 1025;
	private const int HttpPort = 8025;

	private static IContainer _mailpit = null!;
	private static HttpClient _http = null!;

	[Before(hookType: Class)]
	public static async Task StartMailpitAsync()
	{
		_mailpit = new ContainerBuilder(image: "axllent/mailpit:v1.31.3")
			.WithPortBinding(port: SmtpPort, assignRandomHostPort: true)
			.WithPortBinding(port: HttpPort, assignRandomHostPort: true)
			.WithWaitStrategy(waitStrategy: Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(port: HttpPort).ForPath(path: "/api/v1/info")))
			.Build();

		await _mailpit.StartAsync();

		_http = new HttpClient { BaseAddress = new Uri(uriString: $"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(containerPort: HttpPort)}") };
	}

	[After(hookType: Class)]
	public static async Task StopMailpitAsync()
	{
		_http.Dispose();
		await _mailpit.DisposeAsync();
	}

	private static EmailNotificationSender CreateSender() => new EmailNotificationSender(options: new FakeOptionsMonitor<SmtpOptions>(value: new SmtpOptions
	{
		Host = _mailpit.Hostname,
		Port = _mailpit.GetMappedPublicPort(containerPort: SmtpPort),
		From = "alerts@financetracker.test"
	}));

	private static NotificationRecipient NewRecipient() => new NotificationRecipient(
		UserId: Guid.CreateVersion7(),
		Email: Email.Create(value: $"{Guid.CreateVersion7():N}@financetracker.test").Value
	);

	[Test]
	public async Task SendAsync_ShouldDeliverTheSubjectAndBodyToTheRecipient()
	{
		NotificationRecipient recipient = NewRecipient();

		await using (EmailNotificationSender sender = CreateSender())
		{
			await sender.SendAsync(
				recipient: recipient,
				message: new NotificationMessage(Subject: "Budget \"Groceries\": 80% spent", Body: "Spent 8,500.00 of 10,000.00 RUB.")
			);
		}

		List<MailpitInbox.ReceivedMail> mail = await new MailpitInbox(http: _http).GetMailToAsync(address: recipient.Email.Value);

		await Assert.That(value: mail).Count().IsEqualTo(expected: 1);
		await Assert.That(value: mail[0].Subject).IsEqualTo(expected: "Budget \"Groceries\": 80% spent");
		await Assert.That(value: mail[0].Text.Trim()).IsEqualTo(expected: "Spent 8,500.00 of 10,000.00 RUB.");
	}

	[Test]
	public async Task SendAsync_TwiceInOneScope_ShouldDeliverBoth()
	{
		NotificationRecipient recipient = NewRecipient();

		await using (EmailNotificationSender sender = CreateSender())
		{
			await sender.SendAsync(recipient: recipient, message: new NotificationMessage(Subject: "first", Body: "first"));
			await sender.SendAsync(recipient: recipient, message: new NotificationMessage(Subject: "second", Body: "second"));
		}

		List<MailpitInbox.ReceivedMail> mail = await new MailpitInbox(http: _http).GetMailToAsync(address: recipient.Email.Value);

		await Assert.That(value: mail.Select(selector: m => m.Subject).Order().ToList()).IsEquivalentTo(expected: ["first", "second"]).Because(message: """
			The second send finds the connection the first one opened. Reconnecting on an open client, or sending
			on one the first send left in a bad state, would lose the second notification.
		""");
	}

	[Test]
	public async Task DisposeAsync_WithoutASend_ShouldNotThrow()
	{
		EmailNotificationSender sender = CreateSender();

		await sender.DisposeAsync();
	}
}
