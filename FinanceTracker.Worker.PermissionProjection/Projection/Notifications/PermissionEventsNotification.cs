using FinanceTracker.Contracts.Events.Abstraction;
using INotification = MediatR.INotification;

namespace FinanceTracker.Worker.PermissionProjection.Projection.Notifications;

/// <summary>
/// MediatR notification carrying a batch of permission integration events for a single user's
/// permission stream. Published by <see cref="PermissionEventsConsumer"/> and handled by
/// <see cref="PermissionProjection"/>.
/// </summary>
/// <param name="UserId">The user whose permission set this batch of events belongs to.</param>
/// <param name="Events">Ordered list of integration events to apply to the read model.</param>
public sealed record PermissionEventsNotification(
	Guid UserId,
	IReadOnlyList<IIntegrationEvent> Events
) : INotification;
