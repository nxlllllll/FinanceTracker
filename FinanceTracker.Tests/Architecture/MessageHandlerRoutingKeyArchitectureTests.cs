using System.Reflection;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Worker.AccountProjection.Consumer;
using FinanceTracker.Worker.PermissionProjection.Consumer;
using FinanceTracker.Worker.RecurringTransactionProjection.Consumer;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using FinanceTracker.Worker.TransferProjection.Consumer;

namespace FinanceTracker.Tests.Architecture;

public sealed class MessageHandlerRoutingKeyArchitectureTests
{
	private static readonly Assembly[] WorkerAssembliesWithHandlers =
	[
		typeof(AccountEventsConsumer).Assembly,
		typeof(PermissionEventsConsumer).Assembly,
		typeof(AccountTransferConsumer).Assembly,
		typeof(RecurringTransactionConsumer).Assembly
	];

	[Test]
	public async Task EveryMessageHandler_ShouldDeclareARoutingKey()
	{
		IEnumerable<Type> handlerTypes = WorkerAssembliesWithHandlers.Distinct().SelectMany(selector: assembly => assembly.GetTypes()).Where(predicate: type =>
		{
			return type is { IsAbstract: false, IsInterface: false } && type.GetInterfaces().Any(
				predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)
			);
		});

		List<string> violations = handlerTypes.Where(predicate: type => type.GetCustomAttribute<RoutingKeyAttribute>() is null)
			.Select(selector: type => type.FullName ?? type.Name)
			.ToList();

		await Assert.That(value: violations).IsEmpty().Because(message: $"""
			These IMessageHandler<T> implementations are missing [RoutingKey] — RabbitMqListenerService will throw at startup, not at compile time:
			{String.Join(separator: ", ", values: violations)}
		""");
	}
}
