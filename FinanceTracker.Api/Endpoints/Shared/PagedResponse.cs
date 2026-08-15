using FinanceTracker.Core.Results;

namespace FinanceTracker.Api.Endpoints.Shared;

/// <summary>
/// HTTP projection of <see cref="PagedResult{T}"/>.
/// </summary>
public sealed record PagedResponse<TItem>(
	IReadOnlyList<TItem> Items,
	bool HasNextPage,
	DateTimeOffset? NextCursorDate,
	Guid? NextCursorId
);
