using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.Transfer.Commands.CreateTransfer;
using FinanceTracker.Tests.Unit.Helpers;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.Transfer;

public sealed class CreateTransferCommandValidatorTests
{
	private CreateTransferCommandValidator _validator = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_validator = new CreateTransferCommandValidator(
			moneyLimits: new FakeOptionsMonitor<MoneyLimitsOptions>(value: new MoneyLimitsOptions())
		);
	}

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		CreateTransferCommand command = CreateTransferCommandFactory.Create();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		CreateTransferCommand command = CreateTransferCommandFactory.Create(userId: Guid.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyFromAccountId_ShouldHaveError()
	{
		CreateTransferCommand command = CreateTransferCommandFactory.Create(fromAccountId: Guid.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.FromAccountId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyToAccountId_ShouldHaveError()
	{
		CreateTransferCommand command = CreateTransferCommandFactory.Create(toAccountId: Guid.Empty);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.ToAccountId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithZeroAmount_ShouldHaveError()
	{
		CreateTransferCommand command = CreateTransferCommandFactory.Create(amount: 0m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
	}

	[Test]
	public async Task Validate_WithAmountExceedingLimit_ShouldHaveError()
	{
		CreateTransferCommand command = CreateTransferCommandFactory.Create(amount: new MoneyLimitsOptions().MaxAmount + 0.01m);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Amount))).IsTrue();
	}

	[Test]
	public async Task Validate_WithAmountAtLimit_ShouldNotHaveError()
	{
		CreateTransferCommand command = CreateTransferCommandFactory.Create(amount: new MoneyLimitsOptions().MaxAmount);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithSameFromAndToAccountId_ShouldHaveError()
	{
		Guid accountId = Guid.CreateVersion7();
		CreateTransferCommand command = CreateTransferCommandFactory.Create(fromAccountId: accountId, toAccountId: accountId);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.ToAccountId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithTooLongDescription_ShouldHaveError()
	{
		CreateTransferCommand command = CreateTransferCommandFactory.Create(description: new string(c: 'a', count: 256));

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Description))).IsTrue();
	}

	[Test]
	public async Task TheCommand_ShouldNotCarryADateAtAll()
	{
		bool hasDate = typeof(CreateTransferCommand).GetProperties().Any(predicate: property => property.PropertyType == typeof(DateTimeOffset));

		await Assert.That(value: hasDate).IsFalse().Because(message: """
			A transfer has no past to describe. Unlike a transaction, which records something that
			happened outside the system and that only the user can date, a transfer is an operation the
			system performs between two of its own accounts — it happens when the command runs, and a
			caller-supplied date would name an event that never took place. Reintroducing one would also
			hand the caller a free pick of exchange rate, since a cross-currency transfer converts at the
			rate of the date it carries and records that rate as final.
		""");
	}
}
