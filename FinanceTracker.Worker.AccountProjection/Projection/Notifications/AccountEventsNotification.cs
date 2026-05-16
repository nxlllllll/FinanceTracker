using FinanceTracker.Contracts.Events.Account.Abstraction;
using INotification = MediatR.INotification;

namespace FinanceTracker.Worker.AccountProjection.Projection.Notifications;

public sealed record AccountEventsNotification(Guid AccountId, IReadOnlyList<IAccountIntegrationEvent> Events) : INotification;