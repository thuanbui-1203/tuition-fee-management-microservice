using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TuitionService.Application.Abstractions;
using TuitionService.Infrastructure.Persistence;
using TuitionService.Infrastructure.Repositories;

namespace TuitionService.Infrastructure;

/// <summary>Composition root for tuition-service infrastructure concerns.</summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "TuitionDb";

    public static IServiceCollection AddTuitionInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<TuitionDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<ITuitionRepository, TuitionRepository>();
        return services;
    }
}
