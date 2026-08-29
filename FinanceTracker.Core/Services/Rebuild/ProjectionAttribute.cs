namespace FinanceTracker.Core.Services.Rebuild;

[AttributeUsage(validOn: AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ProjectionAttribute(string name, string aggregateType) : Attribute
{
	/// <summary>How the projection is addressed from outside.</summary>
	public string Name { get; } = name;

	/// <summary>The aggregate type in the event log whose events feed this projection.</summary>
	public string AggregateType { get; } = aggregateType;
}
