using FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;
using FinanceTracker.Application.UseCases.Category.Notifications;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class CreateCategoryHandlerTests
{
	private ICategoryWriteRepository _categoryWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPublisher _publisher = null!;
	private CreateCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_publisher = Substitute.For<IPublisher>();
		
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
		
		_handler = new CreateCategoryHandler(
			categoryWriteRepository: _categoryWriteRepository,
			unitOfWork: _unitOfWork,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldCreateCategory()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Еда").Value,
			Type: CategoryType.Expense,
			ParentId: null
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			category: Arg.Is<FinanceTracker.Core.Domains.Category.Category>(c =>
				c.Name == "Еда" &&
				c.Type == CategoryType.Expense &&
				c.IsArchived == false
			),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithParentId_ShouldCreateCategoryWithParentId()
	{
		Guid parentId = Guid.CreateVersion7();
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Рестораны").Value,
			Type: CategoryType.Expense,
			ParentId: parentId
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			category: Arg.Is<FinanceTracker.Core.Domains.Category.Category>(c => c.ParentId == parentId),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldPublishNotification()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Еда").Value,
			Type: CategoryType.Expense,
			ParentId: null
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<CategoryCreatedNotification>(n =>
				n.UserId == command.UserId &&
				n.Name == "Еда" &&
				n.Type == CategoryType.Expense),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}