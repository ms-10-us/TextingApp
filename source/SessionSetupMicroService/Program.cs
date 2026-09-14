using SessionSetupMicroService.DIRegistration;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddProblemDetails();

builder.Services.AddSessionSetup(builder.Configuration);

builder.Services.AddSwaggerGen();

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "ok",
    service = "session-setup"
}));

app.MapGet("/health/ready", async (IServiceProvider services, CancellationToken ct) =>
{
    DBOptions options = services.GetRequiredService<DBOptions>();

    if (string.Equals(options.Provider, "InMemory", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Ok(new
        {
            status = "OK",
            provider = "InMemory"
        });
    }

    try
    {
        await using var connection = await services.GetRequiredService<DbDataSource>().OpenConnectionAsync(ct);
        {
            await using var command = connection.CreateCommand();
            {
                command.CommandText = "select 1";
                await command.ExecuteScalarAsync(ct);
                return Results.Ok(new { 
                    status = "ok", 
                    provider = "Postgres" 
                });
            }
        }
    }
    catch (Exception ex)
    {
        return Results.Problem(
                    title: "SessionSetup is not ready",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "session-setup"
}));

app.MapControllers();

app.Run();
