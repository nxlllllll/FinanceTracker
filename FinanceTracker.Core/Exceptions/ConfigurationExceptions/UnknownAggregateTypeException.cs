namespace FinanceTracker.Core.Exceptions.ConfigurationExceptions;

public sealed class UnknownAggregateTypeException(string message, string aggregateType) : ConfigurationException(message: message)
{
	public string AggregateType { get; init; } = aggregateType;
}