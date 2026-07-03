using FinanceTracker.Application.UseCases.Category.Authorization;
using FinanceTracker.Application.UseCases.Category.Commands.ArchiveCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class CategoryLoaderTests
{
	private ICategoryRepository _categoryRepository = null!;
	private CategoryLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryRepository = Substitute.For<ICategoryRepository>();
		_loader = new CategoryLoader(categoryRepository: _categoryRepository);
	}

	[Test]
	public async Task LoadAsync_WhenCategoryNotFound_ShouldThrowNotFoundException()
	{
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<Category?>(result: null));

		Result<Category, DomainException> result = await _loader.LoadAsync(
			request: new ArchiveCategoryCommand(UserId: Guid.CreateVersion7(), CategoryId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenCategoryBelongsToAnotherUser_ShouldReturnNotFoundException()
	{
		Category category = CategoryFactory.Create().Value!;
		_categoryRepository.GetByIdAsync(
			categoryId: category.Id,
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<Category?>(null));

		Result<Category, DomainException> result = await _loader.LoadAsync(
			request: new ArchiveCategoryCommand(UserId: Guid.CreateVersion7(), CategoryId: category.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenOwner_ShouldReturnCategory()
	{
		Category category = CategoryFactory.Create().Value!;
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		Result<Category, DomainException> result = await _loader.LoadAsync(
			request: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Id).IsEqualTo(expected: category.Id);
	}
}
