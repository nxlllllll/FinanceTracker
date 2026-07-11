namespace FinanceTracker.Core.Repositories.Abstractions;

public interface IEventProjectionSafetyAttribute;

[AttributeUsage(validOn: AttributeTargets.Method, Inherited = false)]
public sealed class EventuallyConsistentCreateAttribute : Attribute, IEventProjectionSafetyAttribute;

[AttributeUsage(validOn: AttributeTargets.Method, Inherited = false)]
public sealed class EventuallyConsistentDeltaAttribute(string ledgerTable) : Attribute, IEventProjectionSafetyAttribute
{
	public string LedgerTable { get; } = ledgerTable;
}

[AttributeUsage(validOn: AttributeTargets.Method, Inherited = false)]
public sealed class EventuallyConsistentAssignmentAttribute(string versionColumn) : Attribute, IEventProjectionSafetyAttribute
{
	public string VersionColumn { get; } = versionColumn;
}
