using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Services;

namespace NotificationService.Application;

/// <summary>Composition root for notification-service application concerns.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEmailContentBuilder, EmailContentBuilder>();
        services.AddScoped<EmailProcessingService>();
        return services;
    }
}
