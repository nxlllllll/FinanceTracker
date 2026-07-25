using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Core.Results;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ValidationException = FinanceTracker.Core.Exceptions.ValidationException;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class ValidationBehaviourTests
{
	public sealed record TestCommand : IRequest<Result<Guid, ValidationException>>;

	private ILogger<ValidationBehaviour<TestCommand, Result<Guid, ValidationException>>> _logger = null!;

	[Before(hookType: Test)]
	public void Setup()
		=> _logger = Substitute.For<ILogger<ValidationBehaviour<TestCommand, Result<Guid, ValidationException>>>>();

	private ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> CreateBehavior(
		params IValidator<TestCommand>[] validators)
	{
		return new ValidationBehaviour<TestCommand, Result<Guid, ValidationException>>(
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
		ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> behaviour = CreateBehavior();
		bool nextCalled = false;

		await behaviour.Handle(
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
		ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> behaviour = CreateBehavior(PassingValidator(), PassingValidator());
		bool nextCalled = false;

		await behaviour.Handle(
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
		ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> behaviour = CreateBehavior(FailingValidator("Name is required."));
		bool nextCalled = false;

		await behaviour.Handle(
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
		ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> behaviour = CreateBehavior(FailingValidator("Name is required."));

		Result<Guid, ValidationException> result = await behaviour.Handle(
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
		ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> behaviour = CreateBehavior(
			FailingValidator("Error one.", "Error two."),
			FailingValidator("Error three.")
		);

		Result<Guid, ValidationException> result = await behaviour.Handle(
			request: new TestCommand(),
			next: _ => Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid())),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();

		await Assert.That(value: result.Error!.Errors).ContainsKey(expectedKey: "field");
		await Assert.That(value: result.Error!.Errors["field"]).Contains(expected: "Error one.");
		await Assert.That(value: result.Error!.Errors["field"]).Contains(expected: "Error two.");
		await Assert.That(value: result.Error!.Errors["field"]).Contains(expected: "Error three.");
	}

	[Test]
	public async Task Handle_WhenOneValidatorPassesAndOneFails_ShouldReturnFailure()
	{
		ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> behaviour = CreateBehavior(
			PassingValidator(),
			FailingValidator("Amount must be positive.")
		);

		Result<Guid, ValidationException> result = await behaviour.Handle(
			request: new TestCommand(),
			next: _ => Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid())),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task Handle_WhenAllValidatorsPass_ShouldReturnNextResult()
	{
		ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> behaviour = CreateBehavior(PassingValidator());
		Guid expected = Guid.CreateVersion7();

		Result<Guid, ValidationException> result = await behaviour.Handle(
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
		ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> behaviour = CreateBehavior(FailingValidator(expectedErrors));

		Result<Guid, ValidationException> result = await behaviour.Handle(
			request: new TestCommand(),
			next: _ => Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid())),
			cancellationToken: CancellationToken.None
		);

		foreach (string error in expectedErrors)
			await Assert.That(value: result.Error!.Errors["field"]).Contains(expected: error);
	}

	private sealed class EnumerateOnceOnly<T>(IReadOnlyList<T> items) : IEnumerable<T>
	{
		private int _enumerations;

		public IEnumerator<T> GetEnumerator()
		{
			if (Interlocked.Increment(location: ref _enumerations) > 1)
				throw new InvalidOperationException(message: "Enumerated more than once.");

			return items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}

	[Test]
	public async Task Handle_WithALazyEnumerableOfValidators_ShouldEnumerateExactlyOnce()
	{
		IValidator<TestCommand>[] underlying = [FailingValidator("Boom.")];
		IEnumerable<IValidator<TestCommand>> lazyValidators = new EnumerateOnceOnly<IValidator<TestCommand>>(items: underlying);

		ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> behaviour = new ValidationBehaviour<TestCommand, Result<Guid, ValidationException>>(
			validators: lazyValidators,
			logger: _logger
		);

		Result<Guid, ValidationException> result = await behaviour.Handle(
			request: new TestCommand(),
			next: _ => Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid())),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue()
			.Because(message: "A second enumeration would have thrown before ever reaching this assertion.");
	}

	[Test]
	public async Task Handle_WhenValidatorFails_ShouldConvertPropertyNameToCamelCase()
	{
		IValidator<TestCommand> validator = Substitute.For<IValidator<TestCommand>>();
		ValidationResult validationResult = new ValidationResult(
			failures: [new ValidationFailure(propertyName: "NewAccountName", errorMessage: "Name is required.")]
		);
		validator.ValidateAsync(context: Arg.Any<ValidationContext<TestCommand>>(), cancellation: Arg.Any<CancellationToken>())
			.Returns(returnThis: validationResult);

		ValidationBehaviour<TestCommand, Result<Guid, ValidationException>> behaviour = CreateBehavior(validator);

		Result<Guid, ValidationException> result = await behaviour.Handle(
			request: new TestCommand(),
			next: _ => Task.FromResult(result: Result<Guid, ValidationException>.Success(value: Guid.NewGuid())),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.Error!.Errors).ContainsKey(expectedKey: "newAccountName");
		await Assert.That(value: result.Error!.Errors).DoesNotContainKey(expectedKey: "NewAccountName");
	}
}
