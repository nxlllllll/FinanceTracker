using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.ChangeRecurringTransactionCurrency;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.ValueObjects;
using FluentValidation.Results;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.RecurringTransaction;

public sealed class ChangeRecurringTransactionCurrencyCommandValidatorTests
{
	private readonly ICurrencyReadRepository _currencyReadRepository;
	private readonly ChangeRecurringTransactionCurrencyCommandValidator _validator;

	public ChangeRecurringTransactionCurrencyCommandValidatorTests()
	{
		_currencyReadRepository = Substitute.For<ICurrencyReadRepository>();
		_currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		_validator = new ChangeRecurringTransactionCurrencyCommandValidator(currencyReadRepository: _currencyReadRepository);
	}

	private static ChangeRecurringTransactionCurrencyCommand ValidCommand(string currencyCode = "USD") => new ChangeRecurringTransactionCurrencyCommand(
		UserId: Guid.CreateVersion7(),
		RecurringTransactionId: Guid.CreateVersion7(),
		Currency: Currency.Reconstitute(value: currencyCode)
	);

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ChangeRecurringTransactionCurrencyCommand command = ValidCommand();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ChangeRecurringTransactionCurrencyCommand command = ValidCommand() with { UserId = Guid.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyRecurringTransactionId_ShouldHaveError()
	{
		ChangeRecurringTransactionCurrencyCommand command = ValidCommand() with { RecurringTransactionId = Guid.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.RecurringTransactionId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithNonExistentCurrency_ShouldHaveError()
	{
		ChangeRecurringTransactionCurrencyCommand command = ValidCommand(currencyCode: "XYZ");
		_currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: false);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.Currency))).IsTrue();
	}
}
