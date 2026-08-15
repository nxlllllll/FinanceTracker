using System.Reflection;
using System.Runtime.CompilerServices;
using FinanceTracker.Application.Behaviours.Notification;
using MediatR;

namespace FinanceTracker.Tests.Architecture;

public sealed class NotificationArchitectureTests
{
	private static readonly Assembly ApplicationAssembly = typeof(IPostCommitNotifications).Assembly;

	private static IReadOnlyList<Type> Notifications => [..ApplicationAssembly.GetTypes()
		.Where(predicate: type => type is { IsClass: true, IsAbstract: false } && typeof(INotification).IsAssignableFrom(c: type))
		.OrderBy(keySelector: type => type.Name)];

	private static IReadOnlySet<Type> HandledNotifications => ApplicationAssembly.GetTypes()
		.Where(predicate: type => type is { IsClass: true, IsAbstract: false })
		.SelectMany(selector: type => type.GetInterfaces())
		.Where(predicate: contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
		.Select(selector: contract => contract.GetGenericArguments()[0])
		.ToHashSet();

	private static bool IsMutable(PropertyInfo property)
	{
		MethodInfo? setter = property.SetMethod;

		if (setter is null || !setter.IsPublic)
			return false;

		return !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(value: typeof(IsExternalInit));
	}

	[Test]
	public async Task EveryNotificationHasSomewhereToGo()
	{
		IReadOnlyList<Type> orphans = [.. Notifications.Where(predicate: notification => !HandledNotifications.Contains(item: notification))];

		await Assert.That(value: orphans.Select(selector: type => type.Name)).IsEmpty()
			.Because(message: "an unhandled notification is published successfully and does nothing, which is indistinguishable from working");
	}
	[Test]
	public async Task TheNotificationsThemselvesAreStillBeingFound()
	{
		await Assert.That(value: Notifications).IsNotEmpty();
		await Assert.That(value: HandledNotifications).IsNotEmpty();
	}

	[Test]
	public async Task NotificationsAreImmutableRecords()
	{
		IReadOnlyList<Type> mutable = [
			..Notifications.Where(
				predicate: notification => notification.GetProperties(bindingAttr: BindingFlags.Public | BindingFlags.Instance
			).Any(predicate: IsMutable))
		];

		await Assert.That(value: mutable.Select(selector: type => type.Name)).IsEmpty()
			.Because(message: "a notification is handed to several handlers in turn, so one of them mutating it would change what the others see");
	}
}
