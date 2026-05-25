namespace FinanceTracker.Contracts.Messages;

public interface IRoutableMessage
{
	string RoutingKey { get; }
}
