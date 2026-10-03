using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Observability.Metrics;
using FinanceTracker.Core.Repositories.Notification;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Notification;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Services.Notification;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notification;

[NotInParallel]
public sealed class NotificationDispatcherTests
{
	private const string TemplateName = "Greeting";

	private IUserAuthRepository _userAuthRepository = null!;
	private INotificationDeliveryRepository _deliveryRepository = null!;
	private INotificationSender _emailSender = null!;
	private User _user = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_user = UserFactory.Create(email: "owner@test.com").Value!;

		_userAuthRepository = Substitute.For<IUserAuthRepository>();
		_userAuthRepository.GetByIdAsync(userId: _user.Id, ct: Arg.Any<CancellationToken>()).Returns(returnThis: _user);

		_deliveryRepository = Substitute.For<INotificationDeliveryRepository>();

		_emailSender = Substitute.For<INotificationSender>();
		_emailSender.Type.Returns(returnThis: NotificationType.Email);
	}

	private NotificationDispatcher CreateDispatcher(params INotificationSender[] senders) => new NotificationDispatcher(
		userAuthRepository: _userAuthRepository,
		notificationDeliveryRepository: _deliveryRepository,
		notificationSenders: senders,
		options: new FakeOptionsMonitor<NotificationOptions>(value: new NotificationOptions
		{
			Templates = new Dictionary<string, NotificationTemplate>
			{
				[TemplateName] = new NotificationTemplate { Subject = "Hello, {Name}", Body = "Your budget is at {Percent}%." }
			}
		}),
		dateProvider: FakeDateProvider.Default,
		logger: NullLogger<NotificationDispatcher>.Instance
	);

	private NotificationRequest Request(string template = TemplateName) => new NotificationRequest(
		EventId: Guid.CreateVersion7(),
		UserId: _user.Id,
		Template: template,
		Values: new Dictionary<string, string> { ["Name"] = "Alex", ["Percent"] = "85" }
	);

	[Test]
	public async Task SendAsync_ShouldSendTheRenderedTemplateToTheUserAndRecordIt()
	{
		NotificationRequest request = Request();

		await CreateDispatcher(_emailSender).SendAsync(request: request);

		await _emailSender.Received(requiredNumberOfCalls: 1).SendAsync(
			recipient: Arg.Is<NotificationRecipient>(r => r!.UserId == _user.Id && r.Email == _user.Email),
			message: Arg.Is<NotificationMessage>(m => m!.Subject == "Hello, Alex" && m.Body == "Your budget is at 85%."),
			ct: Arg.Any<CancellationToken>()
		);

		await _deliveryRepository.Received(requiredNumberOfCalls: 1).RecordAsync(
			eventId: request.EventId,
			type: NotificationType.Email,
			userId: _user.Id,
			deliveredAt: FakeDateProvider.Default.UtcNow,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task SendAsync_WhenTheUserHasNotificationsOff_ShouldNeitherSendNorRecord()
	{
		_user.ChangeNotificationType(newNotificationType: null);

		await CreateDispatcher(_emailSender).SendAsync(request: Request());

		await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(recipient: null!, message: null!);
		await _deliveryRepository.DidNotReceiveWithAnyArgs().RecordAsync(eventId: default, type: default, userId: default, deliveredAt: default);
	}

	[Test]
	public async Task SendAsync_WhenTheUserIsGone_ShouldNeitherSendNorRecord()
	{
		NotificationRequest request = Request() with { UserId = Guid.CreateVersion7() };

		await CreateDispatcher(_emailSender).SendAsync(request: request);

		await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(recipient: null!, message: null!);
	}

	[Test]
	public async Task SendAsync_WhenAlreadyDelivered_ShouldNotSendAgain()
	{
		NotificationRequest request = Request();
		_deliveryRepository.IsDeliveredAsync(eventId: request.EventId, type: NotificationType.Email, ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		await CreateDispatcher(_emailSender).SendAsync(request: request);

		await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(recipient: null!, message: null!);
	}

	[Test]
	public async Task SendAsync_WhenSendingFails_ShouldNotRecordTheDelivery()
	{
		_emailSender.SendAsync(
			recipient: Arg.Any<NotificationRecipient>(),
			message: Arg.Any<NotificationMessage>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(ex: new IOException(message: "SMTP server went away"));

		await Assert.ThrowsAsync<IOException>(action: async () => await CreateDispatcher(_emailSender).SendAsync(request: Request()));

		await _deliveryRepository.DidNotReceiveWithAnyArgs().RecordAsync(eventId: default, type: default, userId: default, deliveredAt: default);
	}

	[Test]
	public async Task SendAsync_WithNoSenderForTheUsersChannel_ShouldThrow()
	{
		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await CreateDispatcher().SendAsync(request: Request())).Because(message: """
			A user whose channel no sender serves is a configuration defect. Returning quietly would ack the
			message and lose the notification; throwing sends it to the dead-letter queue, where it is seen.
		""");
	}

	[Test]
	public async Task SendAsync_WithAnUnknownTemplate_ShouldThrowWithoutSending()
	{
		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await CreateDispatcher(_emailSender).SendAsync(request: Request(template: "Missing")));

		await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(recipient: null!, message: null!);
	}

	private static MetricCollector CollectNotificationMetrics() => new MetricCollector(
		"user_notifications.sent",
		"user_notifications.skipped",
		"user_notifications.failed"
	);

	[Test]
	public async Task SendAsync_WhenSent_ShouldCountItByChannelAndTemplate()
	{
		using MetricCollector metrics = CollectNotificationMetrics();

		await CreateDispatcher(_emailSender).SendAsync(request: Request());

		await Assert.That(value: metrics.Total(
			instrument: "user_notifications.sent",
			(FinanceTrackerMetrics.Tags.Channel, "email"),
			(FinanceTrackerMetrics.Tags.Template, TemplateName)
		)).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task SendAsync_WhenNotificationsAreOff_ShouldCountASkipForThatReason()
	{
		using MetricCollector metrics = CollectNotificationMetrics();
		_user.ChangeNotificationType(newNotificationType: null);

		await CreateDispatcher(_emailSender).SendAsync(request: Request());

		await Assert.That(value: metrics.Total(
			instrument: "user_notifications.skipped",
			(FinanceTrackerMetrics.Tags.Reason, FinanceTrackerMetrics.NotificationSkipReasons.Disabled)
		)).IsEqualTo(expected: 1);
		await Assert.That(value: metrics.Total(instrument: "user_notifications.sent")).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task SendAsync_WhenAlreadyDelivered_ShouldCountASkipForThatReason()
	{
		using MetricCollector metrics = CollectNotificationMetrics();
		NotificationRequest request = Request();
		_deliveryRepository.IsDeliveredAsync(eventId: request.EventId, type: NotificationType.Email, ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		await CreateDispatcher(_emailSender).SendAsync(request: request);

		await Assert.That(value: metrics.Total(
			instrument: "user_notifications.skipped",
			(FinanceTrackerMetrics.Tags.Reason, FinanceTrackerMetrics.NotificationSkipReasons.AlreadyDelivered)
		)).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task SendAsync_WhenSendingFails_ShouldCountTheFailureByChannel()
	{
		using MetricCollector metrics = CollectNotificationMetrics();
		_emailSender.SendAsync(
			recipient: Arg.Any<NotificationRecipient>(),
			message: Arg.Any<NotificationMessage>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(ex: new IOException(message: "SMTP server went away"));

		await Assert.ThrowsAsync<IOException>(action: async () => await CreateDispatcher(_emailSender).SendAsync(request: Request()));

		await Assert.That(value: metrics.Total(
			instrument: "user_notifications.failed",
			(FinanceTrackerMetrics.Tags.Channel, "email")
		)).IsEqualTo(expected: 1).Because(message: """
			The exception sends the message back for redelivery, so nothing is lost yet. The counter is what
			shows the channel failing over and over before the retries run out.
		""");
		await Assert.That(value: metrics.Total(instrument: "user_notifications.sent")).IsEqualTo(expected: 0);
	}
}
