using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.ValueObjects;

public sealed class NameTests
{
    [Test]
    public async Task Create_WithValidName_ShouldSucceed()
    {
        Result<Name, DomainException> result = Name.Create(value: "Карта Сбер");

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value.Value).IsEqualTo(expected: "Карта Сбер");
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
        Result<Name, DomainException> result = Name.Create(value: "  Карта Сбер  ");

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value.Value).IsEqualTo(expected: "Карта Сбер");
    }

    [Test]
    public async Task Create_WithInternalSpaces_ShouldPreserveThem()
    {
        Result<Name, DomainException> result = Name.Create(value: "Карта Тинькофф Блэк");

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value.Value).IsEqualTo(expected: "Карта Тинькофф Блэк");
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
        Name name = Name.Reconstitute(value: "Карта Сбер");

        await Assert.That(value: name.Value).IsEqualTo(expected: "Карта Сбер");
    }

    [Test]
    public async Task ImplicitOperator_ToString_ShouldReturnValue()
    {
        Name name = Name.Reconstitute(value: "Карта Сбер");

        string result = name;

        await Assert.That(value: result).IsEqualTo(expected: "Карта Сбер");
    }

    [Test]
    public async Task ToString_ShouldReturnValue()
    {
        Name name = Name.Reconstitute(value: "Карта Сбер");

        await Assert.That(value: name.ToString()).IsEqualTo(expected: "Карта Сбер");
    }

    [Test]
    public async Task Equality_SameValue_ShouldBeEqual()
    {
        Name a = Name.Reconstitute(value: "Карта Сбер");
        Name b = Name.Reconstitute(value: "Карта Сбер");

        await Assert.That(value: a).IsEqualTo(expected: b);
    }

    [Test]
    public async Task Equality_DifferentValue_ShouldNotBeEqual()
    {
        Name a = Name.Reconstitute(value: "Карта Сбер");
        Name b = Name.Reconstitute(value: "Карта Тинькофф");

        await Assert.That(value: a).IsNotEqualTo(notExpected: b);
    }

    [Test]
    public async Task Equality_SameValueDifferentCase_ShouldNotBeEqual()
    {
        Name a = Name.Reconstitute(value: "Сбер");
        Name b = Name.Reconstitute(value: "сбер");

        await Assert.That(value: a).IsNotEqualTo(notExpected: b);
    }
}