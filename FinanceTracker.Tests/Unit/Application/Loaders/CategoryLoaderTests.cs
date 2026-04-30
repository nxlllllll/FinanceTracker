using FinanceTracker.Application.Categories.Authorization;
using FinanceTracker.Application.Categories.Commands.ArchiveCategory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class CategoryLoaderTests
{
	private ICategoryReadRepository _categoryRepository = null!;
	private CategoryLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryRepository = Substitute.For<ICategoryReadRepository>();
		_loader = new CategoryLoader(categoryReadRepository: _categoryRepository);
	}

	[Test]
	public async Task LoadAsync_WhenCategoryNotFound_ShouldThrowNotFoundException()
	{
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Category.Category?>(result: null));

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new ArchiveCategoryCommand(UserId: Guid.NewGuid(), CategoryId: Guid.NewGuid()),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenCategoryBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create();
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new ArchiveCategoryCommand(UserId: Guid.NewGuid(), CategoryId: category.Id),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenOwner_ShouldReturnCategory()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create();
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		FinanceTracker.Core.Domains.Category.Category result = await _loader.LoadAsync(
			request: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Id).IsEqualTo(expected: category.Id);
	}
}