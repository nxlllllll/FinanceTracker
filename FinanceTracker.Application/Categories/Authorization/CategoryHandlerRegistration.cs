using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Categories.Commands.ArchiveCategory;
using FinanceTracker.Application.Categories.Commands.RenameCategory;
using FinanceTracker.Application.Categories.Commands.UnarchiveCategory;
using FinanceTracker.Core.Domains.Category;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.Categories.Authorization;

internal static class CategoryHandlerRegistration
{
	internal static IServiceCollection RegisterCategoryHandlers(this IServiceCollection services)
	{
		services.AddScoped<CategoryLoader>();
		services.AddScoped<IEntityLoader<ArchiveCategoryCommand, Category>>(sp => sp.GetRequiredService<CategoryLoader>());
		services.AddScoped<IEntityLoader<UnarchiveCategoryCommand, Category>>(sp => sp.GetRequiredService<CategoryLoader>());
		services.AddScoped<IEntityLoader<RenameCategoryCommand, Category>>(sp => sp.GetRequiredService<CategoryLoader>());

		services.AddScoped<IAuthorizedHandler<ArchiveCategoryCommand, Category>, ArchiveCategoryHandler>();
		services.AddScoped<IAuthorizedHandler<UnarchiveCategoryCommand, Category>, UnarchiveCategoryHandler>();
		services.AddScoped<IAuthorizedHandler<RenameCategoryCommand, Category>, RenameCategoryHandler>();

		services.AddScoped<IRequestHandler<ArchiveCategoryCommand>, AuthorizedHandlerAdapter<ArchiveCategoryCommand, Category>>();
		services.AddScoped<IRequestHandler<UnarchiveCategoryCommand>, AuthorizedHandlerAdapter<UnarchiveCategoryCommand, Category>>();
		services.AddScoped<IRequestHandler<RenameCategoryCommand>, AuthorizedHandlerAdapter<RenameCategoryCommand, Category>>();

		return services;
	}
}