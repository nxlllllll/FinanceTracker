using System.Runtime.InteropServices;

namespace FinanceTracker.Tests.Architecture;

public sealed class TimeZoneSupportArchitectureTests
{
	private static readonly string[] RepresentativeZones =
	[
		"Pacific/Auckland",   // UTC+12
		"Pacific/Honolulu",   // UTC-10
		"Europe/Moscow",      // UTC+3
		"America/New_York",   // UTC-5/-4
		"Etc/UTC"             // the default a migration would backfill
	];

	[Test]
	[MethodDataSource(nameof(RepresentativeZoneCases))]
	public async Task IanaTimeZoneIdentifiers_ShouldResolveOnThisHost(string zoneId)
	{
		TimeZoneInfo? zone = null;
		Exception? failure = null;

		try
		{
			zone = TimeZoneInfo.FindSystemTimeZoneById(id: zoneId);
		}
		catch (Exception exception)
		{
			failure = exception;
		}

		await Assert.That(value: zone).IsNotNull().Because(message: $"""
			'{zoneId}' did not resolve on {RuntimeInformation.OSDescription}: {failure?.GetType().Name ?? "no exception"}.

			On Linux this almost always means the image lacks tzdata — the Alpine runtime variants do, and
			the Dockerfiles here deliberately use the Debian-based mcr.microsoft.com/dotnet/aspnet:10.0
			for that reason. A TimeZoneNotFoundException also follows from InvariantGlobalization being
			switched on anywhere in the build.
		""");
	}

	public static IEnumerable<Func<string>> RepresentativeZoneCases()
		=> RepresentativeZones.Select(selector: zone => (Func<string>)(() => zone));
}
