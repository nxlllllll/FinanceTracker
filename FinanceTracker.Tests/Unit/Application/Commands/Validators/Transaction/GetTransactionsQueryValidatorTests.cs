using FinanceTracker.Application.Transactions.Queries.GetTransactions;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transaction;

public sealed class GetTransactionsQueryValidatorTests
{
    private readonly GetTransactionsQueryValidator _validator = new GetTransactionsQueryValidator();

    [Test]
    public async Task Validate_WithValidQuery_ShouldNotHaveErrors()
    {
        GetTransactionsQuery query = new GetTransactionsQuery(AccountId: Guid.NewGuid());

        ValidationResult result = await _validator.ValidateAsync(instance: query);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithValidCursor_ShouldNotHaveErrors()
    {
        GetTransactionsQuery query = new GetTransactionsQuery(
            AccountId: Guid.NewGuid(),
            CursorOccurredAt: DateTime.UtcNow,
            CursorId: Guid.NewGuid()
        );

        ValidationResult result = await _validator.ValidateAsync(instance: query);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithPageSizeZero_ShouldHaveError()
    {
        GetTransactionsQuery query = new GetTransactionsQuery(
            AccountId: Guid.NewGuid(),
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
        GetTransactionsQuery query = new GetTransactionsQuery(
            AccountId: Guid.NewGuid(),
            PageSize: 101
        );

        ValidationResult result = await _validator.ValidateAsync(instance: query);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(query.PageSize)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithCursorIdWithoutCursorOccurredAt_ShouldHaveError()
    {
        GetTransactionsQuery query = new GetTransactionsQuery(
            AccountId: Guid.NewGuid(),
            CursorId: Guid.NewGuid()
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
        GetTransactionsQuery query = new GetTransactionsQuery(
            AccountId: Guid.NewGuid(),
            CursorOccurredAt: DateTime.UtcNow
        );

        ValidationResult result = await _validator.ValidateAsync(instance: query);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(query.CursorId)
        )).IsTrue();
    }
}