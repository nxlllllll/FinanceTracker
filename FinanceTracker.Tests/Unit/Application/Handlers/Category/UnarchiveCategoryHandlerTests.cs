using FinanceTracker.Application.UseCases.Category.Commands.UnarchiveCategory;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class UnarchiveCategoryHandlerTests
{
	private ICategoryWriteRepository _categoryWriteRepository = null!;
	private IPublisher _publisher = null!;
	private UnarchiveCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new UnarchiveCategoryHandler(
			categoryWriteRepository: _categoryWriteRepository,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<UnarchiveCategoryHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WithArchivedCategory_ShouldCallUnarchiveAsync()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true).Value!;

		await _handler.HandleAsync(
			command: new UnarchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			entity: category,
			ct: CancellationToken.None
		);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).UnarchiveAsync(
			categoryId: category.Id,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithArchivedCategory_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true).Value!;

		await _handler.HandleAsync(
			command: new UnarchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			entity: category,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<CategoryUnarchivedNotification>(n => n.CategoryId == category.Id && n.UserId == category.UserId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCategoryNotArchived_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: false).Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new UnarchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			entity: category,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenCategoryNotArchived_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: false).Value!;

		await _handler.HandleAsync(
			command: new UnarchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			entity: category,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<CategoryUnarchivedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}
