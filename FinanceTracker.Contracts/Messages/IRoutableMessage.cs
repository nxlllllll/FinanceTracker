namespace FinanceTracker.Contracts.Messages;

public interface IRoutableMessage
{
	Guid MessageId { get; }
	string RoutingKey { get; }
}
