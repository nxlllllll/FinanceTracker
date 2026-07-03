using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.ValueObjects;

public sealed class NameTests
{
	[Test]
	public async Task Create_WithValidName_ShouldSucceed()
	{
		Result<Name, DomainException> result = Name.Create(value: "Новый счёт");

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Value).IsEqualTo(expected: "Новый счёт");
	}

	[Test]
	public async Task Create_WithEmptyString_ShouldReturnFailure()
	{
		Result<Name, DomainException> result = Name.Create(value: String.Empty);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NameException>();
	}

	[Test]
	public async Task Create_WithWhitespaceOnly_ShouldReturnFailure()
	{
		Result<Name, DomainException> result = Name.Create(value: "   ");

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NameException>();
	}

	[Test]
	public async Task Create_WithLeadingAndTrailingSpaces_ShouldTrimValue()
	{
		Result<Name, DomainException> result = Name.Create(value: "  Новый счёт  ");

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Value).IsEqualTo(expected: "Новый счёт");
	}

	[Test]
	public async Task Create_WithInternalSpaces_ShouldPreserveThem()
	{
		Result<Name, DomainException> result = Name.Create(value: "Новый основной счёт");

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Value).IsEqualTo(expected: "Новый основной счёт");
	}

	[Test]
	public async Task Create_WithSingleCharacter_ShouldSucceed()
	{
		Result<Name, DomainException> result = Name.Create(value: "A");

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Value).IsEqualTo(expected: "A");
	}

	[Test]
	public async Task Reconstitute_ShouldBypassValidation()
	{
		Name name = Name.Reconstitute(value: "Новый счёт");

		await Assert.That(value: name.Value).IsEqualTo(expected: "Новый счёт");
	}

	[Test]
	public async Task ImplicitOperator_ToString_ShouldReturnValue()
	{
		Name name = Name.Reconstitute(value: "Новый счёт");

		string result = name;

		await Assert.That(value: result).IsEqualTo(expected: "Новый счёт");
	}

	[Test]
	public async Task ToString_ShouldReturnValue()
	{
		Name name = Name.Reconstitute(value: "Новый счёт");

		await Assert.That(value: name.ToString()).IsEqualTo(expected: "Новый счёт");
	}

	[Test]
	public async Task Equality_SameValue_ShouldBeEqual()
	{
		Name a = Name.Reconstitute(value: "Новый счёт");
		Name b = Name.Reconstitute(value: "Новый счёт");

		await Assert.That(value: a).IsEqualTo(expected: b);
	}

	[Test]
	public async Task Equality_DifferentValue_ShouldNotBeEqual()
	{
		Name a = Name.Reconstitute(value: "Новый счёт");
		Name b = Name.Reconstitute(value: "Новый основной");

		await Assert.That(value: a).IsNotEqualTo(notExpected: b);
	}

	[Test]
	public async Task Equality_SameValueDifferentCase_ShouldNotBeEqual()
	{
		Name a = Name.Reconstitute(value: "Счёт");
		Name b = Name.Reconstitute(value: "счёт");

		await Assert.That(value: a).IsNotEqualTo(notExpected: b);
	}
}
