using FinanceTracker.Application.UseCases.Budget.Queries.GetBudgets;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Queries.Validators.Budget;

public sealed class GetBudgetsQueryValidatorTests
{
	private readonly GetBudgetsQueryValidator _validator = new GetBudgetsQueryValidator();

	[Test]
	public async Task Validate_WithValidQuery_ShouldNotHaveErrors()
	{
		GetBudgetsQuery query = new GetBudgetsQuery(UserId: Guid.CreateVersion7());

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithPageSizeZero_ShouldHaveError()
	{
		GetBudgetsQuery query = new GetBudgetsQuery(UserId: Guid.CreateVersion7(), PageSize: 0);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(query.PageSize))).IsTrue();
	}

	[Test]
	public async Task Validate_WithPageSizeOver100_ShouldHaveError()
	{
		GetBudgetsQuery query = new GetBudgetsQuery(UserId: Guid.CreateVersion7(), PageSize: 101);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(query.PageSize))).IsTrue();
	}

	[Test]
	public async Task Validate_WithValidCursor_ShouldNotHaveErrors()
	{
		GetBudgetsQuery query = new GetBudgetsQuery(
			UserId: Guid.CreateVersion7(),
			CursorCreatedAt: DateTimeOffset.UtcNow,
			CursorId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithCursorCreatedAtWithoutCursorId_ShouldHaveError()
	{
		GetBudgetsQuery query = new GetBudgetsQuery(UserId: Guid.CreateVersion7(), CursorCreatedAt: DateTimeOffset.UtcNow, CursorId: null);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(query.CursorId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithCursorIdWithoutCursorCreatedAt_ShouldHaveError()
	{
		GetBudgetsQuery query = new GetBudgetsQuery(UserId: Guid.CreateVersion7(), CursorId: Guid.CreateVersion7(), CursorCreatedAt: null);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(query.CursorCreatedAt))).IsTrue();
	}
}
