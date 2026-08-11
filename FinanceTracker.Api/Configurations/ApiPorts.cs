namespace FinanceTracker.Api.Configurations;

public static class ApiPorts
{
	public const int Public = 8080;
	public const int Observability = 9100;
	public static readonly string ObservabilityHost = $"*:{Observability}";
}
