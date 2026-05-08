using FinanceTracker.Application.UseCases.Categories.Queries.GetCategories;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Category;

public sealed class GetCategoriesQueryValidatorTests
{
	private readonly GetCategoriesQueryValidator _validator = new GetCategoriesQueryValidator();

	[Test]
	public async Task Validate_WithValidQuery_ShouldNotHaveErrors()
	{
		GetCategoriesQuery query = new GetCategoriesQuery(UserId: Guid.NewGuid());

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithValidCursor_ShouldNotHaveErrors()
	{
		GetCategoriesQuery query = new GetCategoriesQuery(
			UserId: Guid.NewGuid(),
			CursorCreatedAt: DateTime.UtcNow,
			CursorId: Guid.NewGuid()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithPageSizeZero_ShouldHaveError()
	{
		GetCategoriesQuery query = new GetCategoriesQuery(
			UserId: Guid.NewGuid(),
			PageSize: 0
		);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(query.PageSize)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithPageSizeOver100_ShouldHaveError()
	{
		GetCategoriesQuery query = new GetCategoriesQuery(
			UserId: Guid.NewGuid(),
			PageSize: 101
		);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(query.PageSize)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithCursorIdWithoutCursorCreatedAt_ShouldHaveError()
	{
		GetCategoriesQuery query = new GetCategoriesQuery(
			UserId: Guid.NewGuid(),
			CursorId: Guid.NewGuid()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(query.CursorCreatedAt)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithCursorCreatedAtWithoutCursorId_ShouldHaveError()
	{
		GetCategoriesQuery query = new GetCategoriesQuery(
			UserId: Guid.NewGuid(),
			CursorCreatedAt: DateTime.UtcNow
		);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(query.CursorId)
		)).IsTrue();
	}
}