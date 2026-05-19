using System.Text;
using System.Text.Json.Serialization;
using App;
using App.Catalog;
using App.Catalog.Endpoints;
using App.Data;
using App.Faturamento;
using App.Faturamento.Endpoints;
using App.Faturamento.Motor;
using App.Faturamento.Motor.Unimed;
using App.Identity;
using App.Identity.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions.Remove("exception"));
builder.Services.ConfigureHttpJsonOptions(opt =>
    opt.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SaasService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<UnimedRuleSet>();
builder.Services.AddScoped<NullRuleSet>();
builder.Services.AddScoped<PricingRuleSetFactory>();
builder.Services.AddScoped<GuiaService>();
builder.Services.AddScoped<DemonstrativoService>();
builder.Services.AddScoped<RecursoService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ── Identity (user store only — no cookie sign-in for end-users) ────────────
builder.Services.AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();

// ── Authentication ──────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]!;

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddCookie("External")                           // temp cookie for OAuth handshake
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
        options.SignInScheme = "External";           // store Google identity in temp cookie
        options.CallbackPath = "/api/v1/auth/google/callback"; // under /api/ so Nginx routes it to the backend
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

// ── Authorization policies ──────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SaasOnly", p => p.RequireRole("SaasAdmin"));
    options.AddPolicy("TenantAccess", p => p.RequireRole("TenantAdmin", "SaasAdmin"));
    options.AddPolicy("MedicoAccess", p => p.RequireRole("Medico"));
});

// ── OpenTelemetry ───────────────────────────────────────────────────────────
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

ConfigurationValidator.Validate(app.Configuration);

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseExceptionHandler(exApp => exApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    if (ex is null)
    {
        return;
    }

    var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Honorare.ExceptionHandler");
    logger.LogError(ex, "Unhandled exception on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

    System.Diagnostics.Activity.Current?.AddException(ex);
    System.Diagnostics.Activity.Current?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);

    var (statusCode, detail) = ex switch
    {
        BadHttpRequestException { InnerException: System.Text.Json.JsonException }
            => (StatusCodes.Status422UnprocessableEntity, "Os dados enviados não puderam ser processados. Verifique o formato dos campos."),
        BadHttpRequestException b => (b.StatusCode, b.Message),
        InvalidOperationException ioe => (StatusCodes.Status409Conflict, ioe.Message),
        _ => (StatusCodes.Status500InternalServerError, "Erro interno do servidor."),
    };

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/problem+json";
    await ctx.Response.WriteAsJsonAsync(new { status = statusCode, detail });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options => options.RouteTemplate = "api/{documentName}/openapi.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/api/v1/openapi.json", "Honorare v1");
        options.RoutePrefix = "api/docs";
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseMiddleware<TenantStatusMiddleware>();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapSaasEndpoints();
app.MapAdminEndpoints();
app.MapCatalogEndpoints();
app.MapGuiaEndpoints();
app.MapDemonstrativoEndpoints();
app.MapRecursoEndpoints();
app.MapMedicoEndpoints();
app.MapControllers();

app.Run();
