using App.Data;
using App.Identity;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Honorare API", Version = "v1" });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var otlpEndpoint = new Uri(builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(
            serviceName: "honorare-backend",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "dev")
        .AddEnvironmentVariableDetector())
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation(o => o.RecordException = true)
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = otlpEndpoint))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = otlpEndpoint));

builder.Logging.AddOpenTelemetry(l =>
{
    l.IncludeFormattedMessage = true;
    l.IncludeScopes = true;
    l.AddOtlpExporter(o => o.Endpoint = otlpEndpoint);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options => options.RouteTemplate = "api/{documentName}/openapi.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/api/v1/openapi.json", "Honorare v1");
        options.RoutePrefix = "api/docs";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
