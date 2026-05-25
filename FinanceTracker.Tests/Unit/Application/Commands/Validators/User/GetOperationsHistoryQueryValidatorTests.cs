using FinanceTracker.Application.UseCases.Users.Queries.GetOperationsHistory;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class GetOperationsHistoryQueryValidatorTests
{
	private readonly GetOperationsHistoryQueryValidator _validator = new GetOperationsHistoryQueryValidator();

	[Test]
	public async Task Validate_WithValidQuery_ShouldNotHaveErrors()
	{
		ValidationResult result = await _validator.ValidateAsync(
			instance: new GetOperationsHistoryQuery(UserId: Guid.CreateVersion7())
		);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithValidCursor_ShouldNotHaveErrors()
	{
		ValidationResult result = await _validator.ValidateAsync(
			instance: new GetOperationsHistoryQuery(
				UserId: Guid.CreateVersion7(),
				CursorOccurredAt: DateTimeOffset.UtcNow,
				CursorId: Guid.CreateVersion7()
			)
		);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithPageSizeZero_ShouldHaveError()
	{
		GetOperationsHistoryQuery query = new GetOperationsHistoryQuery(UserId: Guid.CreateVersion7(), PageSize: 0);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(query.PageSize)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithPageSizeOver100_ShouldHaveError()
	{
		GetOperationsHistoryQuery query = new GetOperationsHistoryQuery(UserId: Guid.CreateVersion7(), PageSize: 101);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(query.PageSize)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithCursorIdWithoutCursorOccurredAt_ShouldHaveError()
	{
		GetOperationsHistoryQuery query = new GetOperationsHistoryQuery(
			UserId: Guid.CreateVersion7(),
			CursorId: Guid.CreateVersion7()
		);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(query.CursorOccurredAt)
		)).IsTrue();
	}

	[Test]
	public async Task Validate_WithCursorOccurredAtWithoutCursorId_ShouldHaveError()
	{
		GetOperationsHistoryQuery query = new GetOperationsHistoryQuery(
			UserId: Guid.CreateVersion7(),
			CursorOccurredAt: DateTimeOffset.UtcNow
		);

		ValidationResult result = await _validator.ValidateAsync(instance: query);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(
			predicate: e => e.PropertyName == nameof(query.CursorId)
		)).IsTrue();
	}
}
