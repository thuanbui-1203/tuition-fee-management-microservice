using Microsoft.Extensions.DependencyInjection;
using TuitionService.Application.Services;

namespace TuitionService.Application;

/// <summary>Composition root for tuition-service application concerns.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddTuitionApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TuitionApplicationService>();
        return services;
    }
}
