using FinanceTracker.Application.UseCases.Categories.Commands.ArchiveCategory;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class ArchiveCategoryHandlerTests
{
	private ICategoryWriteRepository _categoryWriteRepository = null!;
	private IRecurringTransactionWriteRepository _recurringTransactionWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ArchiveCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
		_recurringTransactionWriteRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
		_handler = new ArchiveCategoryHandler(
			categoryWriteRepository: _categoryWriteRepository,
			recurringTransactionWriteRepository: _recurringTransactionWriteRepository,
			unitOfWork: _unitOfWork,
			logger: Substitute.For<ILogger<ArchiveCategoryHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WithActiveCategory_ShouldArchiveAndDeactivateRecurring()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			category: category,
			ct: CancellationToken.None
		);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).ArchiveAsync(
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
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			category: category,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task HandleAsync_WhenCategoryAlreadyArchived_ShouldNotCallRepository()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			category: category,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();

		await _categoryWriteRepository.DidNotReceive().ArchiveAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}