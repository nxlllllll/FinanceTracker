using FinanceTracker.Application.Configurations;
using FinanceTracker.Application.Configurations.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.Application.Configurations;

public sealed class BudgetAlertOptionsBindingTests
{
	private static int[] Bind(Dictionary<string, string?> settings)
	{
		IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(initialData: settings).Build();

		ServiceCollection services = new ServiceCollection();
		services.AddSingleton(implementationInstance: configuration);
		services.AddApplication();

		using ServiceProvider provider = services.BuildServiceProvider();

		return provider.GetRequiredService<IOptions<BudgetAlertOptions>>().Value.Thresholds;
	}

	[Test]
	public async Task AHostWithoutTheSectionGetsTheDefaultThresholds()
	{
		int[] thresholds = Bind(settings: []);

		await Assert.That(value: thresholds).IsEquivalentTo(expected: [80, 100]).Because(message: """
			Every host that registers the application layer reads these options, test hosts included.
			A section every one of them has to carry is a section one of them will forget.
		""");
	}

	[Test]
	public async Task ConfiguredThresholdsReplaceTheDefaultRatherThanJoinIt()
	{
		int[] thresholds = Bind(settings: new Dictionary<string, string?>
		{
			[$"{BudgetAlertOptions.SectionName}:{nameof(BudgetAlertOptions.Thresholds)}:0"] = "50"
		});

		await Assert.That(value: thresholds).IsEquivalentTo(expected: [50]).Because(message: """
			The configuration binder appends to an array that already holds values. A default written into
			the property itself would turn this into 80, 100, 50.
		""");
	}
}
