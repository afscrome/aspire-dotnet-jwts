using aspirifriday.ApiService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .UseOtlpExporter()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource(builder.Environment.ApplicationName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        var components = document.Components ??= new OpenApiComponents();
        var securitySchemes = components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        securitySchemes["bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Bearer token for API authentication."
        };

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();
        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();

        if (requiresAuthorization && !allowsAnonymous)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("bearer", null, null)] = []
            });
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddSingleton<IAuthorizationHandler, MinimumAgeAuthorizationHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Over18", policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new MinimumAgeRequirement(18)))
    .AddPolicy("Over21", policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new MinimumAgeRequirement(21)));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarUi();
}

app.MapGet("/", () => Results.Redirect("/scalar/v1"))
    .ExcludeFromDescription();

app.MapGet("/claims", (HttpContext context) =>
{
    var claims = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var claim in context.User.Claims)
    {
        claims[claim.Type] = claim.Value;
    }

    return Results.Ok(claims);
}).RequireAuthorization();

app.MapGet("/Over18Only", () => Results.Ok(new { Message = "Over 18 access granted." }))
    .RequireAuthorization("Over18");

app.MapGet("/Over21Only", () => Results.Ok(new { Message = "Over 21 access granted." }))
    .RequireAuthorization("Over21");


app.Run();
