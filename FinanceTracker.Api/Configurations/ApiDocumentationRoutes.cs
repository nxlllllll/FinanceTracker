namespace FinanceTracker.Api.Configurations;

public static class ApiDocumentationRoutes
{
	public const string DocumentName = "v1";
	public const string OpenApiPrefix = "/openapi";
	public const string ScalarPrefix = "/scalar";
	public const string OpenApiDocument = $"{OpenApiPrefix}/{{documentName}}.json";
	public const string ScalarDocument = $"{ScalarPrefix}/{DocumentName}";
}
