namespace FinanceTracker.Application.Behaviours.Authorization;

/// <summary>
/// A command that optionally carries the version the client last saw
/// </summary>
public interface IHasExpectedVersion
{
	int? ExpectedVersion { get; }
}
