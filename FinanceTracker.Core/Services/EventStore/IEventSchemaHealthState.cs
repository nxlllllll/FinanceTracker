namespace FinanceTracker.Core.Services.EventStore;

public interface IEventSchemaHealthState
{
	bool IsCompatible { get; }
	string? Diagnosis { get; }
	void MarkIncompatible(string diagnosis);
}
