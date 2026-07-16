namespace FinanceTracker.Core.Results;

/// <summary>
/// Cursor-based pagination result. Use <see cref="NextCursorDate"/> and
/// <see cref="NextCursorId"/> as the cursor for the next page request.
/// <param name="Items">The items on the current page.</param>
/// <param name="HasNextPage"><c>true</c> if there are more items beyond this page.</param>
/// <param name="NextCursorDate">Cursor date for the next page. <c>null</c> when <see cref="HasNextPage"/> is <c>false</c>.</param>
/// <param name="NextCursorId">Cursor ID for the next page — used as a tiebreaker when multiple items share the same date.
/// <c>null</c> when <see cref="HasNextPage"/> is <c>false</c>.
/// </param>
/// </summary>
public sealed record PagedResult<T>(
	IReadOnlyList<T> Items,
	bool HasNextPage,
	DateTimeOffset? NextCursorDate,
	Guid? NextCursorId
);
