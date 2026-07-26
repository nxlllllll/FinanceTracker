namespace FinanceTracker.Api.Endpoints.Shared;

/// <summary>Standard body for create-endpoints that only return the new resource's id.</summary>
public sealed record CreatedIdResponse(Guid Id);
