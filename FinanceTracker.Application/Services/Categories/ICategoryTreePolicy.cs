using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.Services.Categories;

public interface ICategoryTreePolicy
{
	/// <summary>
	/// Checks the rules that need the rest of the tree: that the placement introduces no cycle, and
	/// that the deepest resulting leaf stays within the configured ceiling.
	/// </summary>
	Task<Result<Unit, DomainException>> EnsurePlaceableAsync(
		Guid userId,
		Guid? parentId,
		Guid? movingCategoryId = null,
		CancellationToken ct = default
	);
}
