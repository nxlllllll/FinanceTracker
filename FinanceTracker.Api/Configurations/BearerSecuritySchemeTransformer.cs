using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FinanceTracker.Api.Configurations;

internal sealed class BearerSecuritySchemeTransformer(
	IAuthenticationSchemeProvider authenticationSchemeProvider
) : IOpenApiDocumentTransformer
{
	public async Task TransformAsync(
		OpenApiDocument document,
		OpenApiDocumentTransformerContext context,
		CancellationToken cancellationToken)
	{
		IEnumerable<AuthenticationScheme> schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
		if (schemes.All(scheme => scheme.Name != "Bearer"))
			return;

		OpenApiSecurityScheme bearerScheme = new OpenApiSecurityScheme
		{
			Type = SecuritySchemeType.Http,
			Scheme = "bearer",
			BearerFormat = "JWT",
			In = ParameterLocation.Header,
			Description = "Access token from POST /api/v1/auth/login."
		};

		document.Components ??= new OpenApiComponents();
		document.AddComponent(id: "Bearer", componentToRegister: bearerScheme);

		OpenApiSecurityRequirement securityRequirement = new OpenApiSecurityRequirement
		{
			[new OpenApiSecuritySchemeReference(referenceId: "Bearer", hostDocument: document)] = []
		};

		foreach (KeyValuePair<HttpMethod, OpenApiOperation> operation in document.Paths.Values.SelectMany(selector: path => path.Operations!))
		{
			operation.Value.Security ??= new List<OpenApiSecurityRequirement>();
			operation.Value.Security.Add(item: securityRequirement);
		}
	}
}

