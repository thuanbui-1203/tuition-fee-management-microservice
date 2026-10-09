using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OtpService.Application.Abstractions;
using OtpService.Infrastructure.Messaging;
using OtpService.Infrastructure.Outbox;
using OtpService.Infrastructure.Persistence;
using OtpService.Infrastructure.Repositories;
using RabbitMQ.Client;

namespace OtpService.Infrastructure;

/// <summary>Composition root for otp-service infrastructure concerns.</summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "OtpDb";

    public static IServiceCollection AddOtpInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<OtpDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<IOtpRepository, OtpRepository>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microservices.Common.Messaging.RabbitMqOptions>>().Value;
            return new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
            };
        });

        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddHostedService<OutboxRelayHostedService>();
        return services;
    }
}
