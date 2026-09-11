using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.Services.Categories;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Services;

public sealed class CategoryTreePolicyTests
{
	private static readonly Guid UserId = Guid.CreateVersion7();

	private ICategoryReadRepository _categoryReadRepository = null!;
	private CategoryTreePolicy _policy = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();

		_categoryReadRepository.GetAncestorIdsAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Array.Empty<Guid>());

		_categoryReadRepository.GetSubtreeHeightAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);

		_policy = new CategoryTreePolicy(
			categoryReadRepository: _categoryReadRepository,
			options: new FakeOptionsMonitor<CategoryOptions>(value: new CategoryOptions())
		);
	}

	private void Ancestors(Guid categoryId, params Guid[] ancestors) => _categoryReadRepository.GetAncestorIdsAsync(
		categoryId: categoryId,
		userId: UserId,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: ancestors);

	private void Height(Guid categoryId, int height) => _categoryReadRepository.GetSubtreeHeightAsync(
		categoryId: categoryId,
		userId: UserId,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: height);

	[Test]
	public async Task Creating_AtTheRoot_ShouldBeAllowed()
	{
		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _policy.EnsurePlaceableAsync(userId: UserId, parentId: null);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Creating_UnderAParentAtTheCeiling_ShouldFail()
	{
		Guid parentId = Guid.CreateVersion7();
		Ancestors(categoryId: parentId, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _policy.EnsurePlaceableAsync(userId: UserId, parentId: parentId);

		await Assert.That(value: result.Error).IsTypeOf<CategoryDepthExceededException>().Because(message: """
			The parent already sits at the fourth level, so a child of it would be the fifth. Counting the
			parent's own depth is what the ancestor chain is for.
		""");
	}

	[Test]
	public async Task Moving_UnderItsOwnDescendant_ShouldFail()
	{
		Guid categoryId = Guid.CreateVersion7();
		Guid descendantId = Guid.CreateVersion7();
		Ancestors(categoryId: descendantId, categoryId);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _policy.EnsurePlaceableAsync(
			userId: UserId,
			parentId: descendantId,
			movingCategoryId: categoryId
		);

		await Assert.That(value: result.Error).IsTypeOf<CategoryCycleException>().Because(message: """
			A node put under its own descendant detaches that branch from the tree: it becomes reachable
			only from itself, and every walk over it runs forever.
		""");
	}

	[Test]
	public async Task Moving_ShouldMeasureTheCeilingAgainstTheSubtreesDeepestLeaf()
	{
		Guid categoryId = Guid.CreateVersion7();
		Guid parentId = Guid.CreateVersion7();

		Ancestors(categoryId: parentId, Guid.CreateVersion7());
		Height(categoryId: categoryId, height: 2);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _policy.EnsurePlaceableAsync(
			userId: UserId,
			parentId: parentId,
			movingCategoryId: categoryId
		);

		await Assert.That(value: result.Error).IsTypeOf<CategoryDepthExceededException>().Because(message: """
			The moved category itself would land at the third level, which is allowed — its deepest
			descendant would land at the fifth, which is not. Judging the move by the moved node alone
			lets a tall branch past the ceiling.
		""");
	}

	[Test]
	public async Task Moving_ALeafWithRoomLeft_ShouldBeAllowed()
	{
		Guid categoryId = Guid.CreateVersion7();
		Guid parentId = Guid.CreateVersion7();

		Ancestors(categoryId: parentId, Guid.CreateVersion7());
		Height(categoryId: categoryId, height: 0);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _policy.EnsurePlaceableAsync(
			userId: UserId,
			parentId: parentId,
			movingCategoryId: categoryId
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}
}
