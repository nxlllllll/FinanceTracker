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

		services.AddScoped<IAuthorizedHandler<ArchiveCategoryCommand, Category, Guid>, ArchiveCategoryHandler>();
		services.AddScoped<IAuthorizedHandler<UnarchiveCategoryCommand, Category, Guid>, UnarchiveCategoryHandler>();
		services.AddScoped<IAuthorizedHandler<RenameCategoryCommand, Category, Guid>, RenameCategoryHandler>();

		services.AddScoped<IRequestHandler<ArchiveCategoryCommand, Guid>, AuthorizedHandlerAdapter<ArchiveCategoryCommand, Category, Guid>>();
		services.AddScoped<IRequestHandler<UnarchiveCategoryCommand, Guid>, AuthorizedHandlerAdapter<UnarchiveCategoryCommand, Category, Guid>>();
		services.AddScoped<IRequestHandler<RenameCategoryCommand, Guid>, AuthorizedHandlerAdapter<RenameCategoryCommand, Category, Guid>>();

		return services;
	}
}