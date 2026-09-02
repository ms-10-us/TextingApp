using BitcoinWalletMicroService.Dapper;
using BitcoinWalletMicroService.DBSqlite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

WalletDbOptions walletDbOptions = new WalletDbOptions
{
    DbPath = builder.Configuration["Wallet:DbPath"] ?? "/data/wallets.db",
    BusyTimeoutMs = int.TryParse(builder.Configuration["Wallet:BusyTimeoutMs"], out var timeOut)
        ? timeOut
        : 5000
};

builder.Services.AddSingleton(walletDbOptions);
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddSingleton<DbInitializer>();

var app = builder.Build();

DapperConfig.Register();
using (var scope = app.Services.CreateScope())
{
    var schemaPath = Path.Combine(AppContext.BaseDirectory, "SQLScripts", "Schema.sql");
    scope.ServiceProvider.GetRequiredService<DbInitializer>().Initialize(schemaPath);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Bitcoin Wallet API v1");
        options.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
