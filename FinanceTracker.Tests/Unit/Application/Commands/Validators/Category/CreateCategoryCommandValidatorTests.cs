using FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.ValueObjects;
using FluentValidation.Results;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Category;

public sealed class CreateCategoryCommandValidatorTests
{
	private ICategoryReadRepository _categoryReadRepository = null!;
	private CreateCategoryCommandValidator _validator = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();

		_categoryReadRepository.ExistsAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		_validator = new CreateCategoryCommandValidator(categoryReadRepository: _categoryReadRepository);
	}

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Еда").Value,
			Type: CategoryType.Expense,
			ParentId: null
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithValidParentBelongingToUser_ShouldNotHaveErrors()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Еда").Value,
			Type: CategoryType.Expense,
			ParentId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithParentNotBelongingToUser_ShouldHaveError()
	{
		_categoryReadRepository.ExistsAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Еда").Value,
			Type: CategoryType.Expense,
			ParentId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.ParentId)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithInvalidType_ShouldHaveError()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Еда").Value,
			Type: (CategoryType)99,
			ParentId: null
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.Type)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.Empty,
			Name: Name.Create(value: "Еда").Value,
			Type: CategoryType.Expense,
			ParentId: null
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(command.UserId)
		)).IsTrue();
	}
}