using BitcoinWalletMicroService.Dapper;
using BitcoinWalletMicroService.DBSqlite;
using BitcoinWalletMicroService.Orchestrator;
using BitcoinWalletMicroService.Repository;
using BitcoinWalletMicroService.Utilities;

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

builder.Services.AddScoped<IWalletOrchestrator, WalletOrchestrator>();
builder.Services.AddScoped<IMnemonicService, Bip39MnemonicService>();
builder.Services.AddScoped<IKeyDerivationService, Bip32KeyDerivationService>();

string secretEntropy = builder.Configuration["Wallet:SecretEntropy"]
    ?? throw new InvalidOperationException(
        "Wallet:SecretEntropy is not configured. Set WALLET_SECRET_ENTROPY in .env, " +
        "or Wallet__SecretEntropy in the environment / launchSettings.");
builder.Services.AddScoped<ISercretProtector>(_ => new AesGcmSecretProtector(secretEntropy));

builder.Services.AddScoped<IQrCodeService, QrCodeService>();

builder.Services.AddScoped<IWalletRepository, WalletRepository>();

builder.Services.AddScoped<IWalletTransferOrchestrator, WalletTransferOrchestrator>();
builder.Services.AddScoped<ITransactionBuilderService, TransactionBuilderService>();
builder.Services.AddHttpClient<IBlockstreamClient, BlockstreamClient>();
builder.Services.AddScoped<IWalletTransferRepository, WalletTransferRepository>();

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
