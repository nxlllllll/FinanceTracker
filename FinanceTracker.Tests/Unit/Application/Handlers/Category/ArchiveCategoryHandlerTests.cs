using FinanceTracker.Application.UseCases.Category.Commands.ArchiveCategory;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class ArchiveCategoryHandlerTests
{
	private ICategoryWriteRepository _categoryWriteRepository = null!;
	private IRecurringTransactionWriteRepository _recurringTransactionWriteRepository = null!;
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPublisher _publisher = null!;
	private ArchiveCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
		_recurringTransactionWriteRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_publisher = Substitute.For<IPublisher>();
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());
		_handler = new ArchiveCategoryHandler(
			categoryWriteRepository: _categoryWriteRepository,
			recurringTransactionWriteRepository: _recurringTransactionWriteRepository,
			budgetWriteRepository: _budgetWriteRepository,
			unitOfWork: _unitOfWork,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<ArchiveCategoryHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WithActiveCategory_ShouldArchiveCategory()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			user: category,
			ct: CancellationToken.None
		);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).ArchiveAsync(
			categoryId: category.Id,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithActiveCategory_ShouldDeactivateRecurringTransactions()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			user: category,
			ct: CancellationToken.None
		);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).DeactivateByCategoryIdAsync(
			categoryId: category.Id,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithActiveCategory_ShouldDeactivateBudgets()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			user: category,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).DeactivateByCategoryIdAsync(
			categoryId: category.Id,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithActiveCategory_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			user: category,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<CategoryArchivedNotification>(n => n.CategoryId == category.Id && n.UserId == category.UserId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCategoryAlreadyArchived_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true).Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			user: category,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task HandleAsync_WhenCategoryAlreadyArchived_ShouldNotCallRepository()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true).Value!;

		await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			user: category,
			ct: CancellationToken.None
		);

		await _categoryWriteRepository.DidNotReceive().ArchiveAsync(
			categoryId: Arg.Any<Guid>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _budgetWriteRepository.DidNotReceive().DeactivateByCategoryIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _recurringTransactionWriteRepository.DidNotReceive().DeactivateByCategoryIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCategoryAlreadyArchived_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true).Value!;

		await _handler.HandleAsync(
			command: new ArchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			user: category,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<CategoryArchivedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
