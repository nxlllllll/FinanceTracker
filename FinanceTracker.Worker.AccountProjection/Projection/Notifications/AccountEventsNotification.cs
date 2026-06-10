using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Worker.AccountProjection.Consumer;
using INotification = MediatR.INotification;

namespace FinanceTracker.Worker.AccountProjection.Projection.Notifications;

/// <summary>
/// MediatR notification carrying a batch of account integration events for a single aggregate write.
/// Published by <see cref="AccountEventsConsumer"/> and handled by <see cref="AccountProjection"/>.
/// </summary>
/// <param name="AccountId">The account this batch of events belongs to.</param>
/// <param name="Events">Ordered list of integration events to apply to the read model.</param>
public sealed record AccountEventsNotification(Guid AccountId, IReadOnlyList<IAccountIntegrationEvent> Events) : INotification;
