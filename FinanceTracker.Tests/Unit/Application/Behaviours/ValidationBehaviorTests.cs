using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Core.Results;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ValidationException = FinanceTracker.Core.Exceptions.ValidationException;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class ValidationBehaviorTests
{
    public sealed record TestCommand : IRequest<Result<Guid, ValidationException>>;

    private ILogger<ValidationBehavior<TestCommand, Result<Guid, ValidationException>>> _logger = null!;

    [Before(hookType: Test)]
    public void Setup()
        => _logger = Substitute.For<ILogger<ValidationBehavior<TestCommand, Result<Guid, ValidationException>>>>();

    private ValidationBehavior<TestCommand, Result<Guid, ValidationException>> CreateBehavior(
        params IValidator<TestCommand>[] validators)
    {
        return new ValidationBehavior<TestCommand, Result<Guid, ValidationException>>(
            validators: validators,
            logger: _logger
        );
    }

    private static IValidator<TestCommand> PassingValidator()
    {
        IValidator<TestCommand> validator = Substitute.For<IValidator<TestCommand>>();
        validator.ValidateAsync(
            context: Arg.Any<ValidationContext<TestCommand>>(),
            cancellation: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ValidationResult());
        return validator;
    }

    private static IValidator<TestCommand> FailingValidator(params string[] errors)
    {
        IValidator<TestCommand> validator = Substitute.For<IValidator<TestCommand>>();
        ValidationResult result = new ValidationResult(
            failures: errors.Select(selector: e => new ValidationFailure(propertyName: "Field", errorMessage: e))
        );
        validator.ValidateAsync(
            context: Arg.Any<ValidationContext<TestCommand>>(),
            cancellation: Arg.Any<CancellationToken>()
        ).Returns(returnThis: result);
        return validator;
    }

    [Test]
    public async Task Handle_WhenNoValidators_ShouldCallNext()
    {
        ValidationBehavior<TestCommand, Result<Guid, ValidationException>> behavior = CreateBehavior();
        bool nextCalled = false;

        await behavior.Handle(
            request: new TestCommand(),
            next: _ =>
            {
                nextCalled = true;
                return Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid()));
            },
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: nextCalled).IsTrue();
    }

    [Test]
    public async Task Handle_WhenAllValidatorsPass_ShouldCallNext()
    {
        ValidationBehavior<TestCommand, Result<Guid, ValidationException>> behavior = CreateBehavior(PassingValidator(), PassingValidator());
        bool nextCalled = false;

        await behavior.Handle(
            request: new TestCommand(),
            next: _ =>
            {
                nextCalled = true;
                return Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid()));
            },
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: nextCalled).IsTrue();
    }

    [Test]
    public async Task Handle_WhenValidatorFails_ShouldNotCallNext()
    {
        ValidationBehavior<TestCommand, Result<Guid, ValidationException>> behavior = CreateBehavior(FailingValidator("Name is required."));
        bool nextCalled = false;

        await behavior.Handle(
            request: new TestCommand(),
            next: _ =>
            {
                nextCalled = true;
                return Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid()));
            },
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: nextCalled).IsFalse();
    }

    [Test]
    public async Task Handle_WhenValidatorFails_ShouldReturnFailureResult()
    {
        ValidationBehavior<TestCommand, Result<Guid, ValidationException>> behavior = CreateBehavior(FailingValidator("Name is required."));

        Result<Guid, ValidationException> result = await behavior.Handle(
            request: new TestCommand(),
            next: _ => Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid())),
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<ValidationException>();
    }

    [Test]
    public async Task Handle_WhenMultipleValidatorsFail_ShouldAggregateAllErrors()
    {
        ValidationBehavior<TestCommand, Result<Guid, ValidationException>> behavior = CreateBehavior(
            FailingValidator("Error one.", "Error two."),
            FailingValidator("Error three.")
        );

        Result<Guid, ValidationException> result = await behavior.Handle(
            request: new TestCommand(),
            next: _ => Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid())),
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error!.Errors).Contains(expected: "Error one.");
        await Assert.That(value: result.Error!.Errors).Contains(expected: "Error two.");
        await Assert.That(value: result.Error!.Errors).Contains(expected: "Error three.");
    }

    [Test]
    public async Task Handle_WhenOneValidatorPassesAndOneFails_ShouldReturnFailure()
    {
        ValidationBehavior<TestCommand, Result<Guid, ValidationException>> behavior = CreateBehavior(
            PassingValidator(),
            FailingValidator("Amount must be positive.")
        );

        Result<Guid, ValidationException> result = await behavior.Handle(
            request: new TestCommand(),
            next: _ => Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid())),
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: result.IsFailure).IsTrue();
    }

    [Test]
    public async Task Handle_WhenAllValidatorsPass_ShouldReturnNextResult()
    {
        ValidationBehavior<TestCommand, Result<Guid, ValidationException>> behavior = CreateBehavior(PassingValidator());
        Guid expected = Guid.CreateVersion7();

        Result<Guid, ValidationException> result = await behavior.Handle(
            request: new TestCommand(),
            next: _ => Task.FromResult(result: Result<Guid, ValidationException>.Success(value: expected)),
            cancellationToken: CancellationToken.None
        );

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value).IsEqualTo(expected: expected);
    }

    [Test]
    public async Task Handle_WhenValidatorFails_ShouldPassAllErrorsInException()
    {
        string[] expectedErrors = ["Field must not be empty.", "Field must be positive."];
        ValidationBehavior<TestCommand, Result<Guid, ValidationException>> behavior = CreateBehavior(FailingValidator(expectedErrors));

        Result<Guid, ValidationException> result = await behavior.Handle(
            request: new TestCommand(),
            next: _ => Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid())),
            cancellationToken: CancellationToken.None
        );

        foreach (string error in expectedErrors)
            await Assert.That(value: result.Error!.Errors).Contains(expected: error);
    }
}
