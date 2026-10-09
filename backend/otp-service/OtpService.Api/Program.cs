using Microservices.Common.Api;
using Microservices.Common.Messaging;
using Microservices.Common.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using OtpService.Application;
using OtpService.Application.Options;
using OtpService.Infrastructure;
using OtpService.Infrastructure.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithUnifiedValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "OTP Service API (internal)", Version = "v1" });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddOtpApplication();
builder.Services.AddOtpInfrastructure(builder.Configuration);

builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection(OtpOptions.SectionName));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));
builder.Services.Configure<EventEncryptionOptions>(builder.Configuration.GetSection(EventEncryptionOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<InternalApiKeyOptions>(builder.Configuration.GetSection(InternalApiKeyOptions.SectionName));

builder.Services.AddHealthChecks()
    .AddCheck<OtpDatabaseHealthCheck>("postgres", tags: new[] { "ready" })
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => true });

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
    await DatabaseInitializer.MigrateAsync(app.Services, logger);
}

app.Run();

/// <summary>Marker used by <c>WebApplicationFactory</c> in integration tests.</summary>
public partial class Program
{
}
