namespace FinanceTracker.Core.ReadModels;

/// <summary>
/// Marker interface for read model types returned by query repositories.
/// Read models are denormalized projections optimised for read performance —
/// they are never used for domain logic and must not be passed to write operations.
/// </summary>
public interface IReadModel { }