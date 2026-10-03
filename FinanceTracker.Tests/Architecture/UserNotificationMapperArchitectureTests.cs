using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Worker.Notification.Mapping;

namespace FinanceTracker.Tests.Architecture;

public sealed class UserNotificationMapperArchitectureTests
{
	[Test]
	public async Task EveryUserNotification_ShouldHaveAMapperInTheNotificationWorker()
	{
		IEnumerable<Type> notifications = typeof(IUserNotification).Assembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && typeof(IUserNotification).IsAssignableFrom(c: t));

		HashSet<Type> mapped = typeof(IUserNotificationMapper).Assembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && t.BaseType is { IsGenericType: true } baseType
				&& baseType.GetGenericTypeDefinition() == typeof(UserNotificationMapper<>))
			.Select(selector: t => t.BaseType!.GetGenericArguments()[0])
			.ToHashSet();

		List<string> unmapped = notifications.Where(predicate: t => !mapped.Contains(item: t)).Select(selector: t => t.Name).ToList();

		await Assert.That(value: unmapped).IsEmpty().Because(message: $"""
			Published under the notification routing key with nothing to turn them into a message, these would
			fail every delivery and end up dead-lettered: {String.Join(separator: ", ", values: unmapped)}
		""");
	}
}
