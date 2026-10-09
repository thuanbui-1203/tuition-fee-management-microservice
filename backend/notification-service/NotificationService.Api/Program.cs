using Microservices.Common.Api;
using Microservices.Common.Messaging;
using Microservices.Common.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using NotificationService.Application;
using NotificationService.Application.Options;
using NotificationService.Infrastructure;
using NotificationService.Infrastructure.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Notification Service API", Version = "v1" });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddNotificationApplication();
builder.Services.AddNotificationInfrastructure(builder.Configuration);

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.Configure<EventEncryptionOptions>(builder.Configuration.GetSection(EventEncryptionOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

builder.Services.AddHealthChecks()
    .AddCheck<NotificationDatabaseHealthCheck>("postgres", tags: new[] { "ready" })
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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
