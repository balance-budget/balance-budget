using Balance.Data.Helpers;
using Balance.Services;
using Balance.Web;
using Balance.Web.Configuration;
using Balance.Web.Endpoints;
using Balance.Web.Endpoints.Accounts;
using Balance.Web.Endpoints.BankAccounts;
using Balance.Web.Endpoints.BankTransactions;
using Balance.Web.Endpoints.Counterparties;
using Balance.Web.Endpoints.Currencies;
using Balance.Web.Endpoints.JournalEntries;
using Balance.Web.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Logging.AddConsole(builder.Environment);
builder.Configuration.MapConfigurationSources(builder.Environment);
builder.Services.AddBalanceServices(builder.Configuration);
builder.Services.AddBalanceWeb();
builder.WebHost.UseStaticWebAssets();

var app = builder.Build();
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

await app.MigrateDatabase(lifetime.ApplicationStopping);

app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks(
    "/healthz/ready",
    new HealthCheckOptions { Predicate = static c => c.Tags.Contains("readiness") }
);
app.MapStaticAssets();
app.MapHtmx();
app.MapOpenApi();
app.MapScalarApiReference();

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

app.MapCurrencies();
app.MapAccounts();
app.MapCounterparties();
app.MapBankAccounts();
app.MapBankTransactions();
app.MapJournalEntries();

await app.RunAsync(lifetime.ApplicationStopping);

internal partial class Program;
