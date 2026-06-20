using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.ValueObjects;

public sealed class EmailTests
{
    [Test]
    public async Task Create_WithValidEmail_ShouldSucceed()
    {
        Result<Email, DomainException> result = Email.Create(value: "user@example.com");

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value).IsEqualTo(expected: "user@example.com");
    }

    [Test]
    public async Task Create_WithEmptyString_ShouldReturnFailure()
    {
        Result<Email, DomainException> result = Email.Create(value: String.Empty);

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Create_WithWhitespaceOnly_ShouldReturnFailure()
    {
        Result<Email, DomainException> result = Email.Create(value: "   ");

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Create_WithoutAtSign_ShouldReturnFailure()
    {
        Result<Email, DomainException> result = Email.Create(value: "userexample.com");

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Create_WithoutDomain_ShouldReturnFailure()
    {
        Result<Email, DomainException> result = Email.Create(value: "user@");

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Create_WithoutLocalPart_ShouldReturnFailure()
    {
        Result<Email, DomainException> result = Email.Create(value: "@example.com");

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Create_WithoutTld_ShouldReturnFailure()
    {
        Result<Email, DomainException> result = Email.Create(value: "user@example");

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Create_WithConsecutiveDotsInDomain_ShouldReturnFailure()
    {
        Result<Email, DomainException> result = Email.Create(value: "user@example..com");

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Create_WithTrailingDotInDomain_ShouldReturnFailure()
    {
        Result<Email, DomainException> result = Email.Create(value: "user@example.com.");

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Create_WithLeadingDotInDomain_ShouldReturnFailure()
    {
        Result<Email, DomainException> result = Email.Create(value: "user@.example.com");

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Create_WithSpaceInside_ShouldReturnFailure()
    {
        Result<Email, DomainException> result = Email.Create(value: "us er@example.com");

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Create_WithSubdomain_ShouldSucceed()
    {
        Result<Email, DomainException> result = Email.Create(value: "user@mail.example.com");

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value).IsEqualTo(expected: "user@mail.example.com");
    }

    [Test]
    public async Task Create_WithPlusAlias_ShouldSucceed()
    {
        Result<Email, DomainException> result = Email.Create(value: "user+tag@example.com");

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value).IsEqualTo(expected: "user+tag@example.com");
    }

    [Test]
    public async Task Create_WithDotsInLocalPart_ShouldSucceed()
    {
        Result<Email, DomainException> result = Email.Create(value: "first.last@example.com");

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value).IsEqualTo(expected: "first.last@example.com");
    }

    [Test]
    public async Task Reconstitute_ShouldBypassValidation()
    {
        Email email = Email.Reconstitute(value: "user@example.com");

        await Assert.That(value: email).IsEqualTo(expected: "user@example.com");
    }

    [Test]
    public async Task ImplicitOperator_ToString_ShouldReturnValue()
    {
        Email email = Email.Reconstitute(value: "user@example.com");

        await Assert.That(value: email).IsEqualTo(expected: "user@example.com");
    }

    [Test]
    public async Task ToString_ShouldReturnEmailAddress()
    {
        Email email = Email.Reconstitute(value: "user@example.com");

        await Assert.That(value: email).IsEqualTo(expected: "user@example.com");
    }

    [Test]
    public async Task Equality_SameAddress_ShouldBeEqual()
    {
        Email a = Email.Reconstitute(value: "user@example.com");
        Email b = Email.Reconstitute(value: "user@example.com");

        await Assert.That(value: a).IsEqualTo(expected: b);
    }

    [Test]
    public async Task Equality_DifferentAddress_ShouldNotBeEqual()
    {
        Email a = Email.Reconstitute(value: "user@example.com");
        Email b = Email.Reconstitute(value: "other@example.com");

        await Assert.That(value: a).IsNotEqualTo(notExpected: b);
    }

    [Test]
    [Arguments("user@example.com")]
    [Arguments("user+tag@example.com")]
    [Arguments("first.last@example.com")]
    [Arguments("user@mail.example.com")]
    [Arguments("user123@example.org")]
    public async Task Create_WithValidEmails_ShouldSucceed(string email)
    {
        Result<Email, DomainException> result = Email.Create(value: email);

        await Assert.That(value: result.IsSuccess).IsTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments("userexample.com")]
    [Arguments("user@")]
    [Arguments("@example.com")]
    [Arguments("user@example")]
    [Arguments("us er@example.com")]
    [Arguments("user@example..com")]
    [Arguments("user@example.com.")]
    [Arguments("user@.example.com")]
    public async Task Create_WithInvalidEmails_ShouldReturnFailure(string email)
    {
        Result<Email, DomainException> result = Email.Create(value: email);

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }
}