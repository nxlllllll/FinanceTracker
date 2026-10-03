namespace FinanceTracker.Contracts.Events.Abstraction;

public interface IUserNotification : IIntegrationEvent
{
	public const string RoutingKey = "UserNotification";

	Guid UserId { get; }
}
