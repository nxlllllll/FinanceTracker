using FinanceTracker.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Architecture;

public sealed class AuthorizationPolicyCachingArchitectureTests
{
	private const string PermissionPolicyName = "permission:account:write";

	private static PermissionPolicyProvider CreateProvider()
	{
		AuthorizationOptions options = new AuthorizationOptions();

		options.AddPolicy(
			name: AuthorizationExtensions.RootPolicyName,
			configurePolicy: policy => policy.AddRequirements(requirements: new RootRequirement())
		);

		return new PermissionPolicyProvider(options: Options.Create(options: options));
	}

	[Test]
	public async Task PermissionPolicyProvider_ShouldAllowCachingPolicies()
	{
		PermissionPolicyProvider provider = CreateProvider();

		await Assert.That(value: provider.AllowsCachingPolicies).IsTrue().Because(message: """
			AuthorizationMiddleware only keeps its per-endpoint policy cache when the provider says it
			may. The flag is a default interface member returning false, so a provider that never
			mentions it silently opts every endpoint in the application out of the cache and pays for
			AuthorizationPolicy.CombineAsync on every request.
		""");
	}

	[Test]
	public async Task PermissionPolicyProvider_ForTheSamePolicyName_ShouldReturnTheSameInstance()
	{
		PermissionPolicyProvider provider = CreateProvider();

		AuthorizationPolicy? first = await provider.GetPolicyAsync(policyName: PermissionPolicyName);
		AuthorizationPolicy? second = await provider.GetPolicyAsync(policyName: PermissionPolicyName);

		await Assert.That(value: first).IsNotNull().Because(message: """
			A null here would make the reference comparison below vacuous and hide whichever of the two
			calls stopped resolving the name.
		""");

		await Assert.That(value: Object.ReferenceEquals(objA: first, objB: second)).IsTrue().Because(message: """
			AllowsCachingPolicies is a promise that a policy never changes for a given name. Returning a
			fresh instance each time leaves that promise true only by accident — it holds because the
			framework happens to ask once per endpoint, not because this class guarantees it.
		""");
	}

	[Test]
	public async Task PermissionPolicyProvider_ForNamesDifferingOnlyInCase_ShouldBuildSeparatePolicies()
	{
		PermissionPolicyProvider provider = CreateProvider();

		AuthorizationPolicy? lower = await provider.GetPolicyAsync(policyName: PermissionPolicyName);
		AuthorizationPolicy? upper = await provider.GetPolicyAsync(policyName: "permission:ACCOUNT:WRITE");

		string lowerPermission = lower!.Requirements.OfType<PermissionRequirement>().Single().Permission;
		string upperPermission = upper!.Requirements.OfType<PermissionRequirement>().Single().Permission;

		await Assert.That(value: lowerPermission).IsEqualTo(expected: "account:write").Because(message: """
			The prefix is matched case-insensitively but the suffix is passed to PermissionRequirement
			verbatim, and PermissionAuthorizationHandler looks it up in the user's permission set as-is.
		""");

		await Assert.That(value: upperPermission).IsEqualTo(expected: "ACCOUNT:WRITE").Because(message: """
			An ignore-case cache key would collapse these two names onto one entry, so whichever spelling
			arrived first would silently answer for the other and grant or refuse the wrong permission.
		""");
	}

	[Test]
	public async Task PermissionPolicyProvider_ForARegisteredPolicyName_ShouldResolveItFromTheOptions()
	{
		PermissionPolicyProvider provider = CreateProvider();

		AuthorizationPolicy? policy = await provider.GetPolicyAsync(policyName: AuthorizationExtensions.RootPolicyName);

		await Assert.That(value: policy).IsNotNull().Because(message: """
			Policies outside the permission prefix are delegated rather than inherited now. Losing that
			delegation would leave every RequireRoot endpoint without a policy, which the authorization
			middleware reports as a missing-policy exception rather than as a refusal.
		""");

		await Assert.That(value: policy!.Requirements.OfType<RootRequirement>().Any()).IsTrue().Because(message: """
			Resolving to some policy is not enough — it has to be the one registered in
			AuthorizationOptions, otherwise root endpoints would be guarded by the default policy and
			accept any authenticated caller.
		""");
	}

	[Test]
	public async Task AuthorizationPolicyProviders_ShouldNotDeriveFromTheDefaultProvider()
	{
		Type[] derived = typeof(PermissionPolicyProvider).Assembly.GetTypes().Where(predicate: type =>
			type is { IsClass: true, IsAbstract: false } &&
			typeof(IAuthorizationPolicyProvider).IsAssignableFrom(c: type) &&
			typeof(DefaultAuthorizationPolicyProvider).IsAssignableFrom(c: type)
		).ToArray();

		await Assert.That(value: derived).IsEmpty().Because(message: $"""
			DefaultAuthorizationPolicyProvider.AllowsCachingPolicies is
			'GetType() == typeof(DefaultAuthorizationPolicyProvider)', so deriving from it disables
			policy caching for every endpoint and no functional test notices. Compose it as a field and
			delegate instead. Offending types: {String.Join(separator: ", ", values: derived.Select(selector: type => type.Name))}
		""");
	}
}
