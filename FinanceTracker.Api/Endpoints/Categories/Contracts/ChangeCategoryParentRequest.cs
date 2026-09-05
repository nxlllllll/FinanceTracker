namespace FinanceTracker.Api.Endpoints.Categories.Contracts;

public sealed record ChangeCategoryParentRequest(Guid? ParentId);
