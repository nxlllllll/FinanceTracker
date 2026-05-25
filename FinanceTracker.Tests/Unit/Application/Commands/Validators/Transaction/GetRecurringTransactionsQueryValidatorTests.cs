using FinanceTracker.Application.UseCases.RecurringTransactions.Queries.GetRecurringTransactions;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transaction;

public sealed class GetRecurringTransactionsQueryValidatorTests
{
    private readonly GetRecurringTransactionsQueryValidator _validator = new GetRecurringTransactionsQueryValidator();

    [Test]
    public async Task Validate_WithValidQuery_ShouldNotHaveErrors()
    {
        GetRecurringTransactionsQuery query = new GetRecurringTransactionsQuery(UserId: Guid.CreateVersion7());

        ValidationResult result = await _validator.ValidateAsync(instance: query);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithValidCursor_ShouldNotHaveErrors()
    {
        GetRecurringTransactionsQuery query = new GetRecurringTransactionsQuery(
            UserId: Guid.CreateVersion7(),
            CursorCreatedAt: DateTimeOffset.UtcNow,
            CursorId: Guid.CreateVersion7()
        );

        ValidationResult result = await _validator.ValidateAsync(instance: query);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithPageSizeZero_ShouldHaveError()
    {
        GetRecurringTransactionsQuery query = new GetRecurringTransactionsQuery(
            UserId: Guid.CreateVersion7(),
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
        GetRecurringTransactionsQuery query = new GetRecurringTransactionsQuery(
            UserId: Guid.CreateVersion7(),
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
        GetRecurringTransactionsQuery query = new GetRecurringTransactionsQuery(
            UserId: Guid.CreateVersion7(),
            CursorId: Guid.CreateVersion7()
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
        GetRecurringTransactionsQuery query = new GetRecurringTransactionsQuery(
            UserId: Guid.CreateVersion7(),
            CursorCreatedAt: DateTimeOffset.UtcNow
        );

        ValidationResult result = await _validator.ValidateAsync(instance: query);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(query.CursorId)
        )).IsTrue();
    }
}
