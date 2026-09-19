using Quartz;

namespace FinanceTracker.Tests.Unit.Helpers;

public sealed class QuartzProbeSignal
{
	public TaskCompletionSource Fired { get; } = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class QuartzProbeJob(QuartzProbeSignal signal) : IJob
{
	public ValueTask Execute(
		IJobExecutionContext context,
		CancellationToken cancellationToken)
	{
		signal.Fired.TrySetResult();
		return ValueTask.CompletedTask;
	}
}
