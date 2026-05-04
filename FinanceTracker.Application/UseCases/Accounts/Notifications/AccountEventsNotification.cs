using FinanceTracker.Core.Domains.Abstractions;
using INotification = MediatR.INotification;

namespace FinanceTracker.Application.UseCases.Accounts.Notifications;

public sealed record AccountEventsNotification(Guid AccountId, IReadOnlyList<IEvent> Events) : INotification;