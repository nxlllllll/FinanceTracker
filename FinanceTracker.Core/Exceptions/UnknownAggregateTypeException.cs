namespace FinanceTracker.Core.Exceptions;

public sealed class UnknownAggregateTypeException(string message, string aggregateType) : Exception(message: message)
{
	public string AggregateType { get; init; } = aggregateType;
}