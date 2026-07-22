using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.ValueObjects;

public sealed class PermissionTests
{
	[Test]
	public async Task Create_WithAllowedCombination_ShouldSucceed()
	{
		Result<Permission, DomainException> result = Permission.Create(resource: Resource.Account, action: PermissionAction.Write);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Create_WithDisallowedCombination_ShouldFailWithUnknownPermissionException()
	{
		Result<Permission, DomainException> result = Permission.Create(resource: Resource.Balance, action: PermissionAction.Delete);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<UnknownPermissionException>();
	}

	[Test]
	public async Task ToString_ShouldReturnLowercaseResourceColonAction()
	{
		Permission permission = Permission.Create(resource: Resource.Account, action: PermissionAction.Write).Value!;

		await Assert.That(value: permission.ToString()).IsEqualTo(expected: "account:write");
	}

	[Test]
	public async Task CreateFromString_WithValidValue_ShouldRoundTrip()
	{
		Result<Permission, DomainException> result = Permission.Create(value: "account:write");

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Resource).IsEqualTo(expected: Resource.Account);
		await Assert.That(value: result.Value!.Action).IsEqualTo(expected: PermissionAction.Write);
	}

	[Test]
	public async Task CreateFromString_WithMalformedValue_ShouldFail()
	{
		Result<Permission, DomainException> result = Permission.Create(value: "not-a-permission");

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<UnknownPermissionException>();
	}

	[Test]
	public async Task CreateFromString_WithUnknownResource_ShouldFail()
	{
		Result<Permission, DomainException> result = Permission.Create(value: "nonexistent:read");

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<UnknownPermissionException>();
	}
}
