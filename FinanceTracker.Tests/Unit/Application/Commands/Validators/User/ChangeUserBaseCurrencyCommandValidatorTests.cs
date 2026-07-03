using FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.ValueObjects;
using FluentValidation.Results;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class ChangeUserBaseCurrencyCommandValidatorTests
{
	private ICurrencyReadRepository _currencyReadRepository = null!;
	private ChangeUserBaseCurrencyCommandValidator _validator = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_currencyReadRepository = Substitute.For<ICurrencyReadRepository>();

		_currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		_validator = new ChangeUserBaseCurrencyCommandValidator(currencyReadRepository: _currencyReadRepository);
	}

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		ChangeUserBaseCurrencyCommand command = new ChangeUserBaseCurrencyCommand(
			UserId: Guid.CreateVersion7(),
			NewBaseCurrency: Currency.Create(value: "USD").Value
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyUserId_ShouldHaveError()
	{
		ChangeUserBaseCurrencyCommand command = new ChangeUserBaseCurrencyCommand(
			UserId: Guid.Empty,
			NewBaseCurrency: Currency.Create(value: "USD").Value
		);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.UserId))).IsTrue();
	}

	[Test]
	public async Task Validate_WithNonExistentCurrency_ShouldHaveError()
	{
		ChangeUserBaseCurrencyCommand command = new ChangeUserBaseCurrencyCommand(
			UserId: Guid.CreateVersion7(),
			NewBaseCurrency: Currency.Create(value: "XYZ").Value
		);

		_currencyReadRepository.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: false);

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(ChangeUserBaseCurrencyCommand.NewBaseCurrency))).IsTrue();
	}
}
