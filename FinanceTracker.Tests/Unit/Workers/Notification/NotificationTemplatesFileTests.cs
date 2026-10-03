using FinanceTracker.Contracts.Events.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Services.Notification;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Services.Notification;
using FinanceTracker.Worker.Notification.Mapping;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Workers.Notification;

public sealed class NotificationTemplatesFileTests
{
	private static NotificationOptions LoadShippedTemplates()
	{
		IConfiguration configuration = new ConfigurationBuilder()
			.AddJsonFile(path: Path.Combine(AppContext.BaseDirectory, FinanceTracker.Worker.Notification.Program.TemplatesFileName), optional: false)
			.Build();

		return configuration.GetSection(key: NotificationOptions.SectionName).Get<NotificationOptions>()!;
	}

	[Test]
	public async Task TheShippedTemplates_ShouldPassValidation()
		=> await Assert.That(value: new NotificationOptionsValidator().Validate(name: null, options: LoadShippedTemplates()).Succeeded).IsTrue();

	[Test]
	[Arguments(80)]
	[Arguments(100)]
	public async Task EveryBudgetAlertTemplate_ShouldRenderWithTheMappersValues(int threshold)
	{
		BudgetThresholdReachedEvent alert = BudgetThresholdReachedMapperTests.Alert(threshold: threshold, spent: 10_000m);
		IUserNotificationMapper mapper = new BudgetThresholdReachedMapper(categoryReadRepository: Substitute.For<ICategoryReadRepository>());

		NotificationRequest request = await mapper.MapAsync(notification: alert);
		NotificationOptions options = LoadShippedTemplates();

		await Assert.That(value: options.Templates.ContainsKey(key: request.Template)).IsTrue()
			.Because(message: $"the mapper asks for '{request.Template}', and the dispatcher dead-letters a notification whose template is missing");

		NotificationMessage message = NotificationTemplateRenderer.Render(template: options.Templates[request.Template], values: request.Values);

		await Assert.That(value: message.Subject.Contains(value: '{') || message.Body.Contains(value: '{')).IsFalse();
	}
}
