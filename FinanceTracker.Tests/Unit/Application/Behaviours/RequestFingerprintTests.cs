using System.Net;
using System.Text.Json;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.UseCases.Role.Commands.CreateRole;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class RequestFingerprintTests
{
	private const string Password = "S3cret-passw0rd";

	private static readonly Guid AccountId = Guid.CreateVersion7();
	private static readonly Guid UserId = Guid.CreateVersion7();
	private static readonly Guid CategoryId = Guid.CreateVersion7();
	private static readonly DateTimeOffset OccurredAt = new DateTimeOffset(year: 2025, month: 6, day: 1, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

	private static string HashOf(IIdempotentCommand command)
		=> RequestFingerprint.GetHash(fingerprint: command.IdempotencyFingerprint);

	private static CreateTransactionCommand Transaction(decimal amount) => new CreateTransactionCommand(
		AccountId: AccountId,
		UserId: UserId,
		CategoryId: CategoryId,
		Amount: amount,
		Currency: Currency.Create(value: "RUB").Value,
		Direction: DirectionType.Debit,
		Description: null,
		OccurredAt: OccurredAt
	);

	private static RegisterUserCommand Registration(string ipAddress) => new RegisterUserCommand(
		Email: Email.Create(value: "user@example.com").Value,
		Password: Password,
		BaseCurrencyCode: Currency.Create(value: "RUB").Value,
		IpAddress: IPAddress.Parse(ipString: ipAddress)
	);

	private static CreateRoleCommand Role(params Permission[] permissions) => new CreateRoleCommand(
		DisplayName: Name.Create(value: "Auditor").Value,
		Permissions: new HashSet<Permission>(collection: permissions)
	);

	[Test]
	public async Task GetHash_ForTheSameRequestSentTwice_ShouldMatch()
	{
		await Assert.That(value: HashOf(command: Transaction(amount: 100m))).IsEqualTo(expected: HashOf(command: Transaction(amount: 100m)));
	}

	[Test]
	public async Task GetHash_ForADifferentAmount_ShouldDiffer()
	{
		await Assert.That(value: HashOf(command: Transaction(amount: 100m))).IsNotEqualTo(notExpected: HashOf(command: Transaction(amount: 500m)));
	}

	[Test]
	public async Task RegisterUserCommand_Fingerprint_ShouldNotCarryThePassword()
	{
		object fingerprint = ((IIdempotentCommand)Registration(ipAddress: "10.0.0.1")).IdempotencyFingerprint;
		string json = JsonSerializer.Serialize(value: fingerprint, inputType: fingerprint.GetType(), options: FinanceTrackerJsonOptions.Payload);

		await Assert.That(value: json.Contains(value: Password, comparisonType: StringComparison.Ordinal)).IsFalse().Because(message: """
			The hash is stored next to the email for as long as the key lives. A plain SHA-256 over a
			password is cheap to brute-force, so carrying it here would undo the Argon2 in the users table.
		""");
	}

	[Test]
	public async Task RegisterUserCommand_FromAnotherAddress_ShouldHashTheSame()
	{
		await Assert.That(value: HashOf(command: Registration(ipAddress: "10.0.0.1"))).IsEqualTo(expected: HashOf(command: Registration(ipAddress: "2001:db8::1"))).Because(message: """
			The address is filled in by the server and changes when a phone moves between networks. A retry
			from the new one is still the same registration.
		""");
	}

	[Test]
	public async Task CreateRoleCommand_WithPermissionsInAnotherOrder_ShouldHashTheSame()
	{
		Permission read = Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!;
		Permission write = Permission.Create(resource: Resource.Account, action: PermissionAction.Write).Value!;

		await Assert.That(value: HashOf(command: Role(read, write))).IsEqualTo(expected: HashOf(command: Role(write, read))).Because(message: """
			A set has no order, but enumerating one does, and string hashes are randomised per process. Two
			instances of the API would otherwise disagree about whether a retry is the same request.
		""");
	}
}
