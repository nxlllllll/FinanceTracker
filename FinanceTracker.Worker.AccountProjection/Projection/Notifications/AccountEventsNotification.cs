using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Abstractions.ES;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using INotification = MediatR.INotification;

namespace FinanceTracker.Worker.AccountProjection.Projection.Notifications;

public sealed record AccountEventsNotification(Guid AccountId, IReadOnlyList<IEvent> Events) : INotification;