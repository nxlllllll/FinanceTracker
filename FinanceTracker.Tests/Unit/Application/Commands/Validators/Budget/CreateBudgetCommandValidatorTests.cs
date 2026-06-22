using FinanceTracker.Application.UseCases.Budget.Commands.CreateBudget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Budget;

public sealed class CreateBudgetCommandValidatorTests
{
	private ICurrencyReadRepository _currencyReadRepository = null!;
	private ICategoryReadRepository _categoryReadRepository = null!;
	private CreateBudgetCommandValidator _validator = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_currencyReadRepository = Substitute.For<ICurrencyReadRepository>();
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();

		_currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);
		_categoryReadRepository.ExistsAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		_validator = new CreateBudgetCommandValidator(
			currencyReadRepository: _currencyReadRepository,
			categoryReadRepository: _categoryReadRepository
		);
	}

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ValidationResult result = await _validator.ValidateAsync(instance: CreateBudgetCommandFactory.Create());

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		CreateBudgetCommand command = CreateBudgetCommandFactory.Create(userId: Guid.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyCategoryId_ShouldHaveError()
	{
		CreateBudgetCommand command = CreateBudgetCommandFactory.Create(categoryId: Guid.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.CategoryId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithCategoryNotBelongingToUser_ShouldHaveError()
	{
		_categoryReadRepository.ExistsAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		CreateBudgetCommand command = CreateBudgetCommandFactory.Create();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.CategoryId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithNonExistentCurrency_ShouldHaveError()
	{
		CreateBudgetCommand command = CreateBudgetCommandFactory.Create(currency: "XYZ");

		_currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: false);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Currency))).IsTrue();
	}

	[Test]
	[Arguments(0)]
	[Arguments(-1)]
	public async Task Validate_WithNonPositiveAmount_ShouldHaveError(decimal amount)
	{
		CreateBudgetCommand command = CreateBudgetCommandFactory.Create(amount: amount);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEndDateBeforeStartDate_ShouldHaveError()
	{
		DateOnly from = DateOnly.FromDateTime(dateTime: DateTime.UtcNow);
		CreateBudgetCommand command = CreateBudgetCommandFactory.Create(from: from, to: from.AddDays(value: -1));

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.To))).IsTrue();
	}
}