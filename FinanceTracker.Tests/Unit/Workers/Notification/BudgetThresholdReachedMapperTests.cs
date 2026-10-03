using FinanceTracker.Contracts.Events.Budget;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Services.Notification;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.Notification.Mapping;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Workers.Notification;

public sealed class BudgetThresholdReachedMapperTests
{
	private ICategoryReadRepository _categoryReadRepository = null!;
	private IUserNotificationMapper _mapper = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();
		_mapper = new BudgetThresholdReachedMapper(categoryReadRepository: _categoryReadRepository);
	}

	public static BudgetThresholdReachedEvent Alert(
		int threshold,
		decimal spent,
		decimal limit = 10_000m
	) => new BudgetThresholdReachedEvent(
		EventId: Guid.CreateVersion7(),
		BudgetId: Guid.CreateVersion7(),
		UserId: Guid.CreateVersion7(),
		CategoryId: Guid.CreateVersion7(),
		Threshold: threshold,
		Spent: spent,
		Limit: limit,
		Currency: "RUB",
		Version: 1,
		OccurredAt: FakeDateProvider.Default.UtcNow
	);

	private void GivenCategory(BudgetThresholdReachedEvent alert, string name) => _categoryReadRepository.GetByIdAsync(
		categoryId: alert.CategoryId,
		userId: alert.UserId,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: CategoryFactory.CreateReadModel(userId: alert.UserId, name: name));

	[Test]
	public async Task MapAsync_BelowTheLimit_ShouldUseTheThresholdTemplateWithFormattedValues()
	{
		BudgetThresholdReachedEvent alert = Alert(threshold: 80, spent: 8_500m);
		GivenCategory(alert: alert, name: "Groceries");

		NotificationRequest request = await _mapper.MapAsync(notification: alert);

		await Assert.That(value: request.EventId).IsEqualTo(expected: alert.EventId);
		await Assert.That(value: request.UserId).IsEqualTo(expected: alert.UserId);
		await Assert.That(value: request.Template).IsEqualTo(expected: BudgetThresholdReachedMapper.ThresholdReachedTemplate);
		await Assert.That(value: request.Values["Category"]).IsEqualTo(expected: "Groceries");
		await Assert.That(value: request.Values["Threshold"]).IsEqualTo(expected: "80");
		await Assert.That(value: request.Values["Spent"]).IsEqualTo(expected: "8,500.00");
		await Assert.That(value: request.Values["Limit"]).IsEqualTo(expected: "10,000.00");
		await Assert.That(value: request.Values["Currency"]).IsEqualTo(expected: "RUB");
		await Assert.That(value: request.Values["Percent"]).IsEqualTo(expected: "85");
	}

	[Test]
	public async Task MapAsync_AtTheLimit_ShouldUseTheLimitTemplate()
	{
		BudgetThresholdReachedEvent alert = Alert(threshold: 100, spent: 12_000m);
		GivenCategory(alert: alert, name: "Groceries");

		NotificationRequest request = await _mapper.MapAsync(notification: alert);

		await Assert.That(value: request.Template).IsEqualTo(expected: BudgetThresholdReachedMapper.LimitReachedTemplate);
		await Assert.That(value: request.Values["Percent"]).IsEqualTo(expected: "120").Because(message: """
			The threshold says which level was crossed; the percentage says where spending actually is. One
			purchase can carry a budget well past its limit.
		""");
	}

	[Test]
	public async Task MapAsync_WhenTheCategoryIsGone_ShouldStillProduceANotification()
	{
		BudgetThresholdReachedEvent alert = Alert(threshold: 80, spent: 8_500m);

		NotificationRequest request = await _mapper.MapAsync(notification: alert);

		await Assert.That(value: request.Values["Category"]).IsEqualTo(expected: BudgetThresholdReachedMapper.UnknownCategoryName)
			.Because(message: "a category deleted after the purchase is no reason to keep the owner from learning they overspent");
	}
}
