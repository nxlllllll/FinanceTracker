using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.Services.Categories;

public sealed class CategoryTreePolicy(
	ICategoryReadRepository categoryReadRepository,
	IOptionsMonitor<CategoryOptions> options
) : ICategoryTreePolicy
{
	public async Task<Result<Unit, DomainException>> EnsurePlaceableAsync(
		Guid userId,
		Guid? parentId,
		Guid? movingCategoryId = null,
		CancellationToken ct = default)
	{
		int maxDepth = options.CurrentValue.MaxDepth;
		int parentDepth = 0;

		if (parentId is not null)
		{
			if (parentId == movingCategoryId)
				return Result<Unit, DomainException>.Failure(error: new CategoryCycleException(message: "A category cannot be its own parent."));

			IReadOnlyList<Guid> ancestors = await categoryReadRepository.GetAncestorIdsAsync(
				categoryId: parentId.Value,
				userId: userId,
				ct: ct
			);

			if (movingCategoryId is not null && ancestors.Contains(value: movingCategoryId.Value))
				return Result<Unit, DomainException>.Failure(error: new CategoryCycleException(message: "A category cannot be moved under one of its own descendants."));

			parentDepth = ancestors.Count + 1;
		}

		int subtreeHeight = movingCategoryId is null
			? 0
			: await categoryReadRepository.GetSubtreeHeightAsync(categoryId: movingCategoryId.Value, userId: userId, ct: ct);

		int deepestLeaf = parentDepth + 1 + subtreeHeight;

		if (deepestLeaf > maxDepth)
		{
			return Result<Unit, DomainException>.Failure(error: new CategoryDepthExceededException(
				message: $"The category tree may be {maxDepth} levels deep; this would make it {deepestLeaf}.",
				maxDepth: maxDepth
			));
		}

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}
