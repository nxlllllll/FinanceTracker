using FinanceTracker.Application.Behaviours.Validation;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class QueryValidationBehaviorTests
{
    public sealed record TestQuery(string Value) : IRequest<string>;

    private static QueryValidationBehavior<TestQuery, string> CreateBehavior(
        params IValidator<TestQuery>[] validators)
    {
        return new QueryValidationBehavior<TestQuery, string>(
            validators: validators,
            logger: Substitute.For<ILogger<QueryValidationBehavior<TestQuery, string>>>()
        );
    }

    private static IValidator<TestQuery> PassingValidator()
    {
        IValidator<TestQuery> validator = Substitute.For<IValidator<TestQuery>>();
        validator.ValidateAsync(
            context: Arg.Any<ValidationContext<TestQuery>>(),
            cancellation: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ValidationResult());
        return validator;
    }

    private static IValidator<TestQuery> FailingValidator(params string[] errors)
    {
        IValidator<TestQuery> validator = Substitute.For<IValidator<TestQuery>>();
        validator.ValidateAsync(
            context: Arg.Any<ValidationContext<TestQuery>>(),
            cancellation: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ValidationResult(
            failures: errors.Select(selector: e => new ValidationFailure(propertyName: "Value", errorMessage: e))
        ));
        return validator;
    }

    [Test]
    public async Task Handle_WhenNoValidators_ShouldCallNext()
    {
        QueryValidationBehavior<TestQuery, string> behavior = CreateBehavior();
        bool nextCalled = false;

        await behavior.Handle(
            request: new TestQuery(Value: "test"),
            next: _ =>
            {
                nextCalled = true;
                return Task.FromResult(result: "ok");
            },
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: nextCalled).IsTrue();
    }

    [Test]
    public async Task Handle_WhenValidationPasses_ShouldCallNext()
    {
        QueryValidationBehavior<TestQuery, string> behavior = CreateBehavior(PassingValidator());
        bool nextCalled = false;

        await behavior.Handle(
            request: new TestQuery(Value: "test"),
            next: _ =>
            {
                nextCalled = true;
                return Task.FromResult(result: "ok");
            },
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: nextCalled).IsTrue();
    }

    [Test]
    public async Task Handle_WhenValidationPasses_ShouldReturnNextResult()
    {
        QueryValidationBehavior<TestQuery, string> behavior = CreateBehavior(PassingValidator());

        string result = await behavior.Handle(
            request: new TestQuery(Value: "test"),
            next: _ => Task.FromResult(result: "expected"),
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: result).IsEqualTo(expected: "expected");
    }

    [Test]
    public async Task Handle_WhenValidationFails_ShouldThrowValidationException()
    {
        QueryValidationBehavior<TestQuery, string> behavior =
            CreateBehavior(FailingValidator("Value is required."));

        await Assert.ThrowsAsync<ValidationException>(action: async () => await behavior.Handle(
            request: new TestQuery(Value: String.Empty),
            next: _ => Task.FromResult(result: "ok"),
            cancellationToken: CancellationToken.None
        ));
    }

    [Test]
    public async Task Handle_WhenValidationFails_ShouldNotCallNext()
    {
        QueryValidationBehavior<TestQuery, string> behavior =
            CreateBehavior(FailingValidator("Value is required."));

        bool nextCalled = false;

        try
        {
            await behavior.Handle(
                request: new TestQuery(Value: String.Empty),
                next: _ =>
                {
                    nextCalled = true;
                    return Task.FromResult(result: "ok");
                },
                cancellationToken: CancellationToken.None
            );
        }
        catch (ValidationException) { }

        await Assert.That(value: nextCalled).IsFalse();
    }

    [Test]
    public async Task Handle_WhenValidationFails_ShouldThrowWithAllErrors()
    {
        QueryValidationBehavior<TestQuery, string> behavior =
            CreateBehavior(FailingValidator("Error one.", "Error two."));

        ValidationException? exception = null;

        try
        {
            await behavior.Handle(
                request: new TestQuery(Value: String.Empty),
                next: _ => Task.FromResult(result: "ok"),
                cancellationToken: CancellationToken.None
            );
        }
        catch (ValidationException ex)
        {
            exception = ex;
        }

        await Assert.That(value: exception).IsNotNull();
        await Assert.That(value: exception!.Errors.Count()).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task Handle_WhenMultipleValidatorsPass_ShouldCallNext()
    {
        QueryValidationBehavior<TestQuery, string> behavior =
            CreateBehavior(PassingValidator(), PassingValidator());

        bool nextCalled = false;

        await behavior.Handle(
            request: new TestQuery(Value: "test"),
            next: _ =>
            {
                nextCalled = true;
                return Task.FromResult(result: "ok");
            },
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: nextCalled).IsTrue();
    }

    [Test]
    public async Task Handle_WhenOneOfMultipleValidatorsFails_ShouldThrowValidationException()
    {
        QueryValidationBehavior<TestQuery, string> behavior =
            CreateBehavior(PassingValidator(), FailingValidator("Second validator failed."));

        await Assert.ThrowsAsync<ValidationException>(action: async () => await behavior.Handle(
            request: new TestQuery(Value: "test"),
            next: _ => Task.FromResult(result: "ok"),
            cancellationToken: CancellationToken.None
        ));
    }
}
