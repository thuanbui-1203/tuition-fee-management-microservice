using Microservices.Common.Api;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using TuitionService.Application;
using TuitionService.Infrastructure;
using TuitionService.Infrastructure.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithUnifiedValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Tuition Service API", Version = "v1" });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddTuitionApplication();
builder.Services.AddTuitionInfrastructure(builder.Configuration);

builder.Services.Configure<InternalApiKeyOptions>(builder.Configuration.GetSection(InternalApiKeyOptions.SectionName));

builder.Services.AddHealthChecks()
    .AddCheck<TuitionDatabaseHealthCheck>("postgres", tags: new[] { "ready" });

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
