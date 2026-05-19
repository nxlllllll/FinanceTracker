namespace FinanceTracker.Core.Results;

public sealed record PagedResult<T>(
	IReadOnlyList<T> Items,
	bool HasNextPage,
	DateTime? NextCursorDate,
	Guid? NextCursorId
);