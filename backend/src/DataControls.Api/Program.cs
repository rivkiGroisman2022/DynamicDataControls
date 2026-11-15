using System.Text.Json.Serialization;
using DataControls.Api.Infrastructure;
using DataControls.Core.Controls;
using DataControls.Core.Definitions;
using DataControls.Core.Metadata;
using DataControls.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services (Dependency Injection) ----------
builder.Services.AddInfrastructure(builder.Configuration);   // SQL Server implementations
builder.Services.AddSingleton(TimeProvider.System);          // injectable clock (lets tests control the time)
builder.Services.AddScoped<ControlService>();
builder.Services.AddScoped<ControlRunService>();
builder.Services.AddScoped<MetadataService>();
builder.Services.AddScoped<DataControls.Core.Ai.AiAssistantService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        DefinitionJson.Configure(o.JsonSerializerOptions);   // same JSON rules as the definition contract
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

// Invalid input (broken JSON, wrong type) → a Hebrew ProblemDetails response, in the same format as every other error.
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = context =>
    {
        var raw = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .Select(e => (key: e.Key, message: e.Value!.Errors[0].ErrorMessage))
            .ToList();
        // When the body itself could not be read, ASP.NET also reports "the <parameter> field is required" - pure noise.
        var bodyUnreadable = raw.Any(e => e.key.StartsWith('$') || e.key == "");
        var errors = raw
            .Where(e => !(bodyUnreadable && !e.key.StartsWith('$') && e.key != "" && !e.key.Contains('.')))
            .Select(e => new { path = e.key.TrimStart('$', '.'), message = ToHebrew(e.message) })
            .ToList();
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "הבקשה אינה תקינה",
            Detail = "המידע שנשלח אינו במבנה הנדרש."
        };
        problem.Extensions["errors"] = errors;
        return new BadRequestObjectResult(problem);
    };
});

builder.Services.AddProblemDetails(o =>
    o.CustomizeProblemDetails = ctx => ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddCors(o => o.AddPolicy("Frontend", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddOpenApi();

// ---------- HTTP pipeline ----------
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                   // /openapi/v1.json
    app.MapScalarApiReference(o => o.Title = "Data Controls API");   // /scalar - interactive API docs
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

app.Run();

// The framework's binding messages are in English; the API speaks Hebrew to its users.
static string ToHebrew(string message) => message switch
{
    _ when message.Contains("non-empty request body", StringComparison.OrdinalIgnoreCase) => "לא נשלח מידע בבקשה.",
    _ when message.Contains("type discriminator", StringComparison.OrdinalIgnoreCase) => "סוג תנאי לא מוכר (נדרש group או rule).",
    _ when message.Contains("is required", StringComparison.OrdinalIgnoreCase) => "שדה חובה חסר.",
    _ => "הערך שנשלח אינו במבנה הנדרש."
};

// Lets integration tests (WebApplicationFactory) reach the Program class.
public partial class Program;
