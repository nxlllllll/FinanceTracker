using System.Reflection;
using System.Text.RegularExpressions;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Tests.Architecture.Helpers;
using MediatR;

namespace FinanceTracker.Tests.Architecture;

public sealed partial class MultiStatementWriteArchitectureTests
{
	private const string InfrastructureProject = "FinanceTracker.Infrastructure";
	private const string ApplicationProject = "FinanceTracker.Application";
	private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;

	private static readonly HashSet<string> BlockKeywords =
	[
		"if",
		"else",
		"for",
		"foreach",
		"while",
		"do",
		"switch",
		"try",
		"catch",
		"finally",
		"using",
		"lock",
		"fixed"
	];

	[GeneratedRegex(pattern: @"\b(ExecuteDeleteAsync|ExecuteUpdateAsync|ExecuteSqlAsync|ExecuteSqlRawAsync|ExecuteSqlInterpolatedAsync)\s*\(", RegexOptions.Compiled)]
	private static partial Regex ImmediateWrite();
	[GeneratedRegex(pattern: @"\b(AddAsync|AddRangeAsync|UpdateRange|RemoveRange)\s*\(", RegexOptions.Compiled)]
	private static partial Regex DeferredWrite();
	[GeneratedRegex(pattern: @"(?<name>\w+)\s*\([^;{}]*\)\s*\{", RegexOptions.Singleline)]
	private static partial Regex Signature();

	private static bool CallsMethod(string source, string methodName)
		=> Regex.IsMatch(input: source, pattern: $@"\.\s*{Regex.Escape(str: methodName)}\s*\(");

	private static IReadOnlyDictionary<string, string> HandlerSources()
	{
		Dictionary<string, string> sources = [];

		foreach (string file in SourceScan.FilesIn(projectName: ApplicationProject))
		{
			string typeName = Path.GetFileNameWithoutExtension(path: file);

			if (!sources.ContainsKey(key: typeName))
				sources[typeName] = SourceScan.StripComments(source: File.ReadAllText(path: file));
		}

		return sources;
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<string>> FindMultiStatementWriteMethods()
	{
		Dictionary<string, IReadOnlyList<string>> result = [];

		foreach (string file in SourceScan.FilesIn(projectName: InfrastructureProject))
		{
			string source = SourceScan.StripComments(source: File.ReadAllText(path: file));
			string typeName = Path.GetFileNameWithoutExtension(path: file);

			List<string> methods = MethodBodies(source: source)
				.Where(predicate: method => !BlockKeywords.Contains(item: method.Name))
				.Where(predicate: method => IsMultiStatementWrite(body: method.Body))
				.Select(selector: method => method.Name)
				.Distinct(comparer: StringComparer.Ordinal)
				.ToList();

			if (methods.Count != 0)
				result[typeName] = methods;
		}

		return result;
	}

	private static bool IsMultiStatementWrite(string body)
	{
		int immediate = ImmediateWrite().Count(input: body);
		int deferred = DeferredWrite().Count(input: body);

		return immediate >= 2 || (immediate >= 1 && deferred >= 1);
	}

	private static IEnumerable<Type> AllHandlerTypes()
	{
		Type requestHandlerOpen = typeof(IRequestHandler<,>);
		Type authorizedHandlerOpen4 = typeof(IAuthorizedHandler<,,,>);
		Type authorizedHandlerOpen3 = typeof(IAuthorizedHandler<,,>);

		return ApplicationAssembly.GetTypes().Where(predicate: t =>
			t is { IsClass: true, IsAbstract: false } &&
			!t.Name.StartsWith(value: "AuthorizedHandlerAdapter", comparisonType: StringComparison.Ordinal) &&
			t.GetInterfaces().Any(predicate: i => i.IsGenericType && (
				i.GetGenericTypeDefinition() == requestHandlerOpen ||
				i.GetGenericTypeDefinition() == authorizedHandlerOpen4 ||
				i.GetGenericTypeDefinition() == authorizedHandlerOpen3
			))
		);
	}

	private static IEnumerable<(string Name, string Body)> MethodBodies(string source)
	{
		foreach (Match match in Signature().Matches(input: source))
		{
			int open = source.IndexOf(value: '{', startIndex: match.Index + match.Length - 1);
			if (open < 0)
				continue;

			int depth = 0;
			int end = -1;

			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{')
					depth++;
				else if (source[i] == '}' && --depth == 0)
				{
					end = i;
					break;
				}
			}

			if (end > open)
				yield return (match.Groups["name"].Value, source[open..end]);
		}
	}

	[Test]
	public async Task HandlersCallingAMultiStatementWrite_ShouldDependOnIUnitOfWork()
	{
		IReadOnlyDictionary<string, IReadOnlyList<string>> hazards = FindMultiStatementWriteMethods();

		await Assert.That(value: hazards).IsNotEmpty().Because(
			message: "No multi-statement write methods were found at all — the source scan is almost certainly looking in the wrong place, so this test would pass vacuously."
		);

		IReadOnlyDictionary<string, string> handlerSources = HandlerSources();

		List<string> violations = [];

		foreach (Type handlerType in AllHandlerTypes())
		{
			ConstructorInfo? constructor = handlerType.GetConstructors().FirstOrDefault();
			if (constructor is null)
				continue;

			Type[] dependencies = constructor.GetParameters().Select(selector: p => p.ParameterType).ToArray();

			if (dependencies.Contains(value: typeof(IUnitOfWork)))
				continue;

			if (!handlerSources.TryGetValue(key: handlerType.Name, value: out string? source))
			{
				violations.Add(item: $"{handlerType.Name} has no matching source file under {ApplicationProject} — " +
					"the call-site check cannot run, so this handler is unverified rather than clean.");
				continue;
			}

			foreach (Type dependency in dependencies.Where(predicate: d => d.IsInterface))
			{
				string implementationName = dependency.Name.StartsWith(value: "I", comparisonType: StringComparison.Ordinal)
					? dependency.Name[1..]
					: dependency.Name;

				if (!hazards.TryGetValue(key: implementationName, out IReadOnlyList<string>? methods))
					continue;

				List<string> called = methods.Where(predicate: method => CallsMethod(source: source, methodName: method)).ToList();

				if (called.Count == 0)
					continue;

				violations.Add(item: $"{handlerType.Name} calls {dependency.Name}.{String.Join(separator: "/", values: called)} " +
					$"but does not depend on IUnitOfWork. {implementationName} writes across several statements there. " +
					"Wrap the call in unitOfWork.ExecuteInTransactionAsync, or the statements are applied one by one.");
			}
		}

		await Assert.That(value: violations).IsEmpty()
			.Because(message: String.Join(separator: "\n", values: violations));
	}

	[Test]
	public async Task TheScan_ShouldStillRecognizeRoleRepository()
	{
		IReadOnlyDictionary<string, IReadOnlyList<string>> hazards = FindMultiStatementWriteMethods();

		await Assert.That(value: hazards.ContainsKey(key: "RoleRepository")).IsTrue().Because(message:
			"RoleRepository.ReplacePermissionsAsync deletes then re-inserts, and DeleteAsync issues three deletes — " +
			"if the scan stops seeing that, it has stopped detecting the exact shape it was written for."
		);

		IReadOnlyList<string> roleRepositoryMethods = hazards["RoleRepository"];

		await Assert.That(value: roleRepositoryMethods.All(predicate: name => !String.IsNullOrEmpty(value: name))).IsTrue().Because(message:
			"At least one extracted method name is empty, which means the name capture in Signature() is not being read. " +
			"Every name then flows into CallsMethod as an empty pattern that matches nothing, so the rule reports no " +
			"violation regardless of the code — passing without checking anything."
		);

		await Assert.That(value: roleRepositoryMethods.Contains(value: "ReplacePermissionsAsync")).IsTrue().Because(message:
			"The scan found hazardous methods in RoleRepository but not this one by name. Asserting on the key alone " +
			"leaves the extracted names unverified, which is exactly how a broken capture stays invisible."
		);
	}

	[Test]
	public async Task TheCallSiteFilter_ShouldSeparateWritersFromReaders()
	{
		IReadOnlyDictionary<string, string> handlerSources = HandlerSources();

		await Assert.That(value: handlerSources.ContainsKey(key: "DeleteRoleHandler")).IsTrue()
			.Because(message: "DeleteRoleHandler's source was not found — without it the call-site check silently degrades into 'unverified'.");

		await Assert.That(value: CallsMethod(source: handlerSources["DeleteRoleHandler"], methodName: "DeleteAsync")).IsTrue()
			.Because(message: "DeleteRoleHandler does call the multi-statement delete. If the filter stops seeing it, every handler looks clean and the rule enforces nothing.");

		await Assert.That(value: handlerSources.ContainsKey(key: "GetRolesHandler")).IsTrue()
			.Because(message: "GetRolesHandler's source was not found — it is the reference read-only handler for this check.");

		await Assert.That(value: CallsMethod(source: handlerSources["GetRolesHandler"], methodName: "ReplacePermissionsAsync")).IsFalse()
			.Because(message: "GetRolesHandler only queries. Flagging it would mean the rule is keying off injection again instead of the call site.");
	}
}
