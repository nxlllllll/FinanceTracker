using System.Runtime.CompilerServices;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;

namespace FinanceTracker.Tests.Architecture.Helpers;

public static class SwitchExhaustivenessChecker
{
	public static async Task<IReadOnlyList<string>> FindUnhandledAsync(
		IEnumerable<Type> candidateTypes,
		Func<object, Task> invoke)
	{
		List<string> unhandled = [];

		foreach (Type type in candidateTypes)
		{
			object instance = RuntimeHelpers.GetUninitializedObject(type: type);

			try
			{
				await invoke(instance);
			}
			catch (UnknownEventException ex) when (ex.EventType == type)
			{
				unhandled.Add(item: type.Name);
			}
			catch { /* Any other exception means a real case matched and was attempted — not our concern here */ }
		}

		return unhandled;
	}
}