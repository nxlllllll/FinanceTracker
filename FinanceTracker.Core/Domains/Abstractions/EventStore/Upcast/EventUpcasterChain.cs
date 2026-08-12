namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

/// <summary>
/// The version range a registered upcaster chain covers for one event type.
/// </summary>
/// <param name="FromVersion">Lowest stored version the chain accepts.</param>
/// <param name="ToVersion">Version the chain produces after every step has run.</param>
public readonly record struct EventUpcasterChain(int FromVersion, int ToVersion);
