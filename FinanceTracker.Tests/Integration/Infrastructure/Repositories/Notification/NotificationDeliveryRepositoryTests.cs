using FinanceTracker.Core.Domains.User;
using FinanceTracker.Infrastructure.Database.Repositories.Notification;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Notification;

public sealed class NotificationDeliveryRepositoryTests : DatabaseFixture
{
	private NotificationDeliveryRepository _repository = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
	{
		_repository = new NotificationDeliveryRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	[Test]
	public async Task IsDeliveredAsync_BeforeAnyRecord_ShouldBeFalse()
	{
		await Assert.That(value: await _repository.IsDeliveredAsync(eventId: Guid.CreateVersion7(), type: NotificationType.Email)).IsFalse();
	}

	[Test]
	public async Task RecordAsync_ShouldMakeTheDeliveryVisible()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid eventId = Guid.CreateVersion7();

		await _repository.RecordAsync(eventId: eventId, type: NotificationType.Email, userId: userId, deliveredAt: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: await _repository.IsDeliveredAsync(eventId: eventId, type: NotificationType.Email)).IsTrue();
	}

	[Test]
	public async Task RecordAsync_WhenAlreadyRecorded_ShouldKeepOneRowWithoutFailing()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid eventId = Guid.CreateVersion7();

		await _repository.RecordAsync(eventId: eventId, type: NotificationType.Email, userId: userId, deliveredAt: FakeDateProvider.Default.UtcNow);
		await _repository.RecordAsync(eventId: eventId, type: NotificationType.Email, userId: userId, deliveredAt: FakeDateProvider.Default.UtcNow.AddMinutes(minutes: 1));

		int rows = await Context.NotificationDeliveries.CountAsync(predicate: d => d.EventId == eventId);

		await Assert.That(value: rows).IsEqualTo(expected: 1).Because(message: """
			The notification is sent before it is recorded. A redelivered message that finds the record already
			written by a parallel attempt has nothing left to do; failing there would dead-letter a message whose
			notification went out.
		""");
	}

	[Test]
	public async Task RecordAsync_ShouldNotMarkOtherEventsDelivered()
	{
		Guid userId = await _userBuilder.CreateAsync();

		await _repository.RecordAsync(eventId: Guid.CreateVersion7(), type: NotificationType.Email, userId: userId, deliveredAt: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: await _repository.IsDeliveredAsync(eventId: Guid.CreateVersion7(), type: NotificationType.Email)).IsFalse();
	}
}
