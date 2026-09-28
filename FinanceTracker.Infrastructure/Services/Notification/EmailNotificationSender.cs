using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Services.Notification;
using FinanceTracker.Infrastructure.Configurations.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace FinanceTracker.Infrastructure.Services.Notification;

public sealed class EmailNotificationSender(
	IOptionsMonitor<SmtpOptions> options
) : INotificationSender, IAsyncDisposable
{
	private readonly SmtpClient _client = new SmtpClient();

	public NotificationType Type => NotificationType.Email;

	public async Task SendAsync(
		NotificationRecipient recipient,
		NotificationMessage message,
		CancellationToken ct = default)
	{
		SmtpOptions smtp = options.CurrentValue;

		if (!_client.IsConnected)
		{
			await _client.ConnectAsync(
				host: smtp.Host,
				port: smtp.Port,
				options: SecureSocketOptions.Auto,
				cancellationToken: ct
			);

			if (!String.IsNullOrEmpty(value: smtp.UserName))
			{
				await _client.AuthenticateAsync(
					userName: smtp.UserName,
					password: smtp.Password ?? String.Empty,
					cancellationToken: ct
				);
			}
		}

		await _client.SendAsync(
			message: BuildMail(smtp: smtp, recipient: recipient, message: message), 
			cancellationToken: ct
		);
	}

	private static MimeMessage BuildMail(
		SmtpOptions smtp,
		NotificationRecipient recipient,
		NotificationMessage message)
	{
		MimeMessage mail = new MimeMessage();

		mail.From.Add(address: MailboxAddress.Parse(text: smtp.From));
		mail.To.Add(address: MailboxAddress.Parse(text: recipient.Email.Value));
		mail.Subject = message.Subject;
		mail.Body = new TextPart(format: TextFormat.Plain) { Text = message.Body };

		return mail;
	}
	
	public async ValueTask DisposeAsync()
	{
		if (_client.IsConnected)
			await _client.DisconnectAsync(quit: true);

		_client.Dispose();
	}
}