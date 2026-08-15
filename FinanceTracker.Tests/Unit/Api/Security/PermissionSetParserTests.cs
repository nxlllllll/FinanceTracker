using FinanceTracker.Api.Security;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Api.Security;

public sealed class PermissionSetParserTests
{
	[Test]
	public async Task AWellFormedSetIsParsed()
	{
		Result<IReadOnlySet<Permission>, ValidationException> result = PermissionSetParser.Parse(
			raw: new HashSet<string> { "account:read", "account:write" }
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value?.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task AnEmptySetIsParsedIntoNothing()
	{
		Result<IReadOnlySet<Permission>, ValidationException> result = PermissionSetParser.Parse(raw: new HashSet<string>());

		await Assert.That(value: result.IsSuccess).IsTrue()
			.Because(message: "a role carrying no permissions is a role, not a malformed request");

		await Assert.That(value: result.Value).IsEmpty();
	}

	[Test]
	public async Task AMalformedEntryIsRejected()
	{
		Result<IReadOnlySet<Permission>, ValidationException> result = PermissionSetParser.Parse(
			raw: new HashSet<string> { "account:read", "nonsense" }
		);

		await Assert.That(value: result.IsFailure).IsTrue()
			.Because(message: "one unusable entry must not be silently dropped from a set that grants access");
	}

	[Test]
	public async Task EveryMalformedEntryIsReportedTogether()
	{
		Result<IReadOnlySet<Permission>, ValidationException> result = PermissionSetParser.Parse(
			raw: new HashSet<string> { "nonsense", "account:fly", "also:nonsense:here" }
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error!.Errors?.Count).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task EachComplaintIsFiledUnderTheEntryThatCausedIt()
	{
		Result<IReadOnlySet<Permission>, ValidationException> result = PermissionSetParser.Parse(
			raw: new HashSet<string> { "account:read", "nonsense" }
		);

		await Assert.That(value: result.Error!.Errors).ContainsKey(expectedKey: "nonsense");
		await Assert.That(value: result.Error!.Errors).DoesNotContainKey(expectedKey: "account:read");
	}

	[Test]
	public async Task NothingIsReturnedWhenAnyEntryFails()
	{
		Result<IReadOnlySet<Permission>, ValidationException> result = PermissionSetParser.Parse(
			raw: new HashSet<string> { "account:read", "nonsense" }
		);

		await Assert.That(value: result.Value).IsNull()
			.Because(message: "a partially applied permission set is a worse outcome than a rejected one");
	}
}
