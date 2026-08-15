using System.Reflection;
using FinanceTracker.Api.Configurations;
using FinanceTracker.Api.Http;
using FinanceTracker.Api.Routing;
using FinanceTracker.Infrastructure.Services.Token;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Architecture;

public sealed class RouteNameArchitectureTests
{
	private static IEnumerable<T> InstancesOf<T>()
	{
		return typeof(Api.Program).Assembly.GetTypes()
			.Where(predicate: type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(targetType: typeof(T)))
			.Select(selector: type => (T)Activator.CreateInstance(type: type)!);
	}

	private static IEndpointRouteBuilder MapEverything()
	{
		WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

		builder.Services.AddSingleton(implementationInstance: Substitute.For<ICurrentUserProvider>());
		builder.Services.AddSingleton(implementationInstance: Substitute.For<ISender>());
		builder.Services.AddSingleton(implementationInstance: Options.Create(options: new JwtOptions()));

		WebApplication app = builder.Build();
		IEndpointRouteBuilder routeBuilder = app;

		routeBuilder.MapEndpoints(
			groups: InstancesOf<IEndpointGroup>(),
			endpoints: InstancesOf<IEndpoint>(),
			options: new ApiRoutingOptions()
		);

		return routeBuilder;
	}

	private static List<string> MappedNames()
	{
		return [.. MapEverything().DataSources.SelectMany(selector: source => source.Endpoints)
			.Select(selector: endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
			.Where(predicate: name => name is not null)
			.Select(selector: name => name!)];
	}

	private static IReadOnlyList<string> DeclaredNames()
	{
		return [.. typeof(RouteNames).GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(predicate: field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
			.Select(selector: field => (string)field.GetRawConstantValue()!)];
	}

	[Test]
	public async Task NoTwoEndpointsAnswerToTheSameName()
	{
		List<string> duplicates = [.. MappedNames()
			.GroupBy(keySelector: name => name, comparer: StringComparer.Ordinal)
			.Where(predicate: group => group.Count() > 1)
			.Select(selector: group => group.Key)];

		await Assert.That(value: duplicates).IsEmpty()
			.Because(message: "a link would still be generated, which makes this the failure that reaches a client rather than a log");
	}

	[Test]
	public async Task EveryDeclaredNameIsAttachedToAnEndpoint()
	{
		IReadOnlyList<string> mapped = MappedNames();

		List<string> unattached = [.. DeclaredNames().Where(predicate: name => !mapped.Contains(value: name))];

		await Assert.That(value: unattached).IsEmpty()
			.Because(message: "a name nothing answers to is a Location header waiting to throw the first time its resource is created");
	}

	[Test]
	public async Task EveryAttachedNameComesFromTheDeclaredList()
	{
		IReadOnlyList<string> declared = DeclaredNames();

		List<string> improvised = [.. MappedNames().Where(predicate: name => !declared.Contains(value: name))];

		await Assert.That(value: improvised).IsEmpty()
			.Because(message: "naming an endpoint with a literal puts the string in two places again, and only one of them gets renamed");
	}

	[Test]
	public async Task TheNamesAreStillBeingFound()
	{
		await Assert.That(value: DeclaredNames()).IsNotEmpty();
		await Assert.That(value: MappedNames()).IsNotEmpty();
	}
}
