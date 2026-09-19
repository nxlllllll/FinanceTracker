using FinanceTracker.Application.Configurations.Options;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.Application.Configurations;

public sealed class BudgetAlertOptionsValidatorTests
{
	private readonly BudgetAlertOptionsValidator _validator = new BudgetAlertOptionsValidator();

	private ValidateOptionsResult Validate(params int[] thresholds)
		=> _validator.Validate(name: null, options: new BudgetAlertOptions { Thresholds = thresholds });

	[Test]
	public async Task TheDefaultThresholdsAreAccepted()
	{
		await Assert.That(value: Validate(80, 100).Succeeded).IsTrue();
	}

	[Test]
	public async Task NoThresholdsAreRefused()
	{
		await Assert.That(value: Validate().Failed).IsTrue()
			.Because(message: "an empty list is what a missing section binds to, so starting with it would switch the feature off without anyone deciding to");
	}

	[Test]
	public async Task AZeroThresholdIsRefused()
	{
		await Assert.That(value: Validate(0, 100).Failed).IsTrue()
			.Because(message: "every budget has reached 0% of its limit from the start, so it could never be crossed");
	}

	[Test]
	public async Task AThresholdAboveTheLimitIsRefused()
	{
		await Assert.That(value: Validate(80, 150).Failed).IsTrue()
			.Because(message: "100% already says the limit is exceeded; the owner does not need to be told again by how much");
	}

	[Test]
	public async Task ARepeatedThresholdIsRefused()
	{
		await Assert.That(value: Validate(80, 80, 100).Failed).IsTrue();
	}
}
