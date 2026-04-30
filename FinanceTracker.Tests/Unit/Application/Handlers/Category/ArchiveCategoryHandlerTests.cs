using FinanceTracker.Application.Categories.Commands.ArchiveCategory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class ArchiveCategoryHandlerTests
{
	private ICategoryRepository _categoryRepository = null!;
	private IRecurringTransactionWriteRepository _recurringTransactionWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ArchiveCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryRepository = Substitute.For<ICategoryRepository>();
		_recurringTransactionWriteRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_handler = new ArchiveCategoryHandler(
			categoryRepository: _categoryRepository,
			recurringTransactionWriteRepository: _recurringTransactionWriteRepository,
			unitOfWork: _unitOfWork
		);
	}

	[Test]
	public async Task HandleAsync_WithActiveCategory_ShouldArchiveAndDeactivateRecurring()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create();

		await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			category: category,
			ct: CancellationToken.None
		);

		await _categoryRepository.Received(requiredNumberOfCalls: 1).ArchiveAsync(
			categoryId: category.Id,
			ct: Arg.Any<CancellationToken>()
		);
		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).DeactivateByCategoryIdAsync(
			categoryId: category.Id,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCategoryAlreadyArchived_ShouldThrowArchivingException()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true);

		await Assert.That(action: async () => await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			category: category,
			ct: CancellationToken.None
		)).Throws<ArchivingException>();
	}

	[Test]
	public async Task HandleAsync_WhenCategoryAlreadyArchived_ShouldNotCallRepository()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true);

		await Assert.That(action: async () => await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			category: category,
			ct: CancellationToken.None
		)).Throws<ArchivingException>();

		await _categoryRepository.DidNotReceive().ArchiveAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}