using Microsoft.AspNetCore.Mvc;
using StaySphere.Api.Contracts;
using StaySphere.Api.Middleware;
using StaySphere.Application;
using StaySphere.Infrastructure;
using StaySphere.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();

// Return the same error envelope for automatic model-binding failures.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

        var payload = new ApiErrorResponse(
            StatusCodes.Status400BadRequest,
            "ValidationFailed",
            "One or more validation errors occurred.",
            errors,
            context.HttpContext.TraceIdentifier);

        return new BadRequestObjectResult(payload);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "StaySphere API", Version = "v1" });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.Logger.LogInformation("StaySphere API starting in {Environment} environment.", app.Environment.EnvironmentName);

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Make the root URL land on the Swagger UI too.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.UseCors("frontend");
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.Logger.LogInformation("StaySphere API ready.");

app.Run();

/// <summary>Exposed so an integration-test host can reference the entry point later.</summary>
public partial class Program;
