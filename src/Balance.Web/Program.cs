using Balance.Configuration.Helpers;
using Balance.Data.Helpers;
using Balance.Services;
using Balance.Web;
using Balance.Web.Configuration;
using Balance.Web.Endpoints.Accounts;
using Balance.Web.Endpoints.BankAccounts;
using Balance.Web.Endpoints.BankTransactions;
using Balance.Web.Endpoints.Counterparties;
using Balance.Web.Endpoints.Currencies;
using Balance.Web.Endpoints.Dashboard;
using Balance.Web.Endpoints.JournalEntries;
using Balance.Web.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

// Detect design time runs and tests
var isOpenApiGenerator = builder.Environment.IsDesignTime();
var isIntegrationTest = builder.Environment.IsIntegrationTest();

builder.Logging.AddConsole(builder.Environment);
builder.Configuration.MapConfigurationSources(builder.Environment);
builder.Services.AddBalanceServices(builder.Configuration, builder.Environment);
builder.Services.AddBalanceWeb();

var app = builder.Build();
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

await app.MigrateDatabase(lifetime.ApplicationStopping);

// Serve Balance.Web.Client SPA
app.MapStaticAssets();
app.MapFallbackToFile("index.html");

// Prefix all Balance.Web endpoints with /api
var api = app.MapGroup("/api");
api.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false });
api.MapHealthChecks(
    "/healthz/ready",
    new HealthCheckOptions { Predicate = static c => c.Tags.Contains("readiness") }
);

api.MapOpenApi();
api.MapScalarApiReference(
    "/docs",
    options =>
    {
        options.WithOpenApiRoutePattern("/api/openapi/{documentName}.json");
        options.WithTitle("Balance API");
    }
);

api.MapCurrencies();
api.MapAccounts();
api.MapCounterparties();
api.MapBankAccounts();
api.MapBankTransactions();
api.MapJournalEntries();
api.MapDashboard();

// Middleware pipeline, order matters here
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseForwardedHeaders();
app.UseDefaultFiles();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

await app.RunAsync(lifetime.ApplicationStopping);

internal partial class Program;
