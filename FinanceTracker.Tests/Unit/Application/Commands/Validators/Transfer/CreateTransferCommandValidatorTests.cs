using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.Transfer.Commands;
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
			dateProvider: FakeDateProvider.Default,
			moneyLimits: new FakeOptionsMonitor<MoneyLimitsOptions>(value: new MoneyLimitsOptions()),
			backdating: new FakeOptionsMonitor<BackdatingOptions>(value: new BackdatingOptions())
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
	public async Task Validate_WithFutureDate_ShouldHaveError()
	{
		CreateTransferCommand command = new CreateTransferCommand(
			UserId: Guid.CreateVersion7(),
			FromAccountId: Guid.CreateVersion7(),
			ToAccountId: Guid.CreateVersion7(),
			Amount: 500m,
			Description: null,
			OccurredAt: FakeDateProvider.Default.UtcNow.AddDays(days: 1)
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.OccurredAt))).IsTrue();
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
	public async Task Validate_WithADateInsideTheBackdatingWindow_ShouldNotHaveErrors()
	{
		CreateTransferCommand command = CreateTransferCommandFactory.Create(
			occurredAt: FakeDateProvider.Default.UtcNow.AddMonths(months: -2)
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue().Because(message: """
			Entering an operation weeks or a couple of months late is the ordinary case the window
			exists to allow. A bound tight enough to reject it would just push people to lie about
			the date.
		""");
	}

	[Test]
	public async Task Validate_WithADateBeyondTheBackdatingWindow_ShouldHaveError()
	{
		CreateTransferCommand command = CreateTransferCommandFactory.Create(
			occurredAt: FakeDateProvider.Default.UtcNow.AddMonths(months: -4)
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.OccurredAt))).IsTrue().Because(message: """
			A cross-currency transfer converts at the rate of the date it carries, and that rate is
			recorded as final. Without a lower bound the date is a free pick among every rate ever
			recorded, and the resulting balances stop describing anything real.
		""");
	}
}
