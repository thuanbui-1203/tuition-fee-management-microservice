using Microservices.Common.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using OtpService.Application.Abstractions;
using OtpService.Application.Security;
using OtpService.Application.Services;

namespace OtpService.Application;

/// <summary>Composition root for otp-service application concerns.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddOtpApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRateLimiter, FixedWindowRateLimiter>();
        services.AddSingleton<IOtpGenerator, CryptoSecureOtpGenerator>();
        services.AddSingleton<IOtpHasher, Pbkdf2OtpHasher>();
        services.AddScoped<OtpApplicationService>();
        return services;
    }
}
