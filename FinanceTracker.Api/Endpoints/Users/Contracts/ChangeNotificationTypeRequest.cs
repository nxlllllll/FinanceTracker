using FinanceTracker.Core.Domains.User;

namespace FinanceTracker.Api.Endpoints.Users.Contracts;

public sealed record ChangeNotificationTypeRequest(NotificationType? Type);
