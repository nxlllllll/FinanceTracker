using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Infrastructure.Services.Rebuild.Account;
using FinanceTracker.Tests.Architecture.Helpers;
using FinanceTracker.Worker.AccountProjection.Projection;
using NSubstitute;

namespace FinanceTracker.Tests.Architecture;

/// <summary>
/// <see cref="AccountDomainEventApplier"/> (used by the rebuild service) and <see cref="AccountEventApplier"/>
/// (used by the live projection worker) both dispatch on event type via a switch expression. See
/// <see cref="SwitchExhaustivenessChecker"/> for why this needs a behavioural check rather than reflection,
/// and <see cref="DomainArchitectureTests.Account_ApplyDispatch_ShouldRouteEveryAccountEvent"/> for the
/// same check applied to <c>Account.Apply()</c> itself.
/// </summary>
public sealed class AccountEventApplierArchitectureTests
{
	private static readonly Type[] AccountDomainEventTypes = typeof(IEvent).Assembly.GetTypes().Where(predicate: t =>
		t is { IsClass: true, IsAbstract: false } &&
		typeof(IEvent).IsAssignableFrom(c: t) &&
		t.Namespace == "FinanceTracker.Core.Domains.Account.Events"
	).ToArray();

	private static readonly Type[] AccountIntegrationEventTypes = typeof(IIntegrationEvent).Assembly.GetTypes().Where(predicate: t =>
		t is { IsClass: true, IsAbstract: false } &&
		typeof(IIntegrationEvent).IsAssignableFrom(c: t) &&
		t.Namespace == "FinanceTracker.Contracts.Events.Account"
	).ToArray();

	[Test]
	public async Task AccountDomainEventApplier_ShouldHandleAllAccountDomainEvents()
	{
		AccountDomainEventApplier applier = new AccountDomainEventApplier(repository: Substitute.For<IAccountWriteRepository>());

		IReadOnlyList<string> unhandled = await SwitchExhaustivenessChecker.FindUnhandledAsync(
			candidateTypes: AccountDomainEventTypes,
			invoke: instance => applier.ApplyAsync(@event: (IEvent)instance, ct: CancellationToken.None)
		);

		await Assert.That(value: unhandled).IsEmpty()
			.Because(message: $"AccountDomainEventApplier is missing handlers for: {String.Join(separator: ", ", values: unhandled)}");
	}

	[Test]
	public async Task AccountEventApplier_ShouldHandleAllAccountIntegrationEvents()
	{
		AccountEventApplier applier = new AccountEventApplier(repository: Substitute.For<IAccountWriteRepository>());

		IReadOnlyList<string> unhandled = await SwitchExhaustivenessChecker.FindUnhandledAsync(
			candidateTypes: AccountIntegrationEventTypes,
			invoke: instance => applier.ApplyAsync(@event: (IIntegrationEvent)instance, ct: CancellationToken.None)
		);

		await Assert.That(value: unhandled).IsEmpty()
			.Because(message: $"AccountEventApplier is missing handlers for: {String.Join(separator: ", ", values: unhandled)}");
	}
}
