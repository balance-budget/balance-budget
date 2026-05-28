using Balance.Configuration.Helpers;
using Balance.Data.Helpers;
using Balance.Integration.Ing;
using Balance.Services;
using Balance.Services.BankTransactions;
using Balance.Web;
using Balance.Web.Auth;
using Balance.Web.Endpoints.Accounts;
using Balance.Web.Endpoints.Admin;
using Balance.Web.Endpoints.Auth;
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

builder.Logging.AddConsole(builder.Environment);
builder.Services.AddBalanceServices(builder.Configuration, builder.Environment);
builder.Services.AddBalanceIntegrationIng();
builder.Services.AddBalanceWeb(builder.Configuration);

var app = builder.Build();
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

await app.MigrateDatabaseAsync(lifetime.ApplicationStopping);

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

// Serve Balance.Web.Client SPA
app.MapStaticAssets();
app.MapFallbackToFile("index.html");

// Prefix all Balance.Web endpoints with /api. The group defaults to RequireAuthorization
// + the AntiforgeryEndpointFilter; individual endpoints opt back to anonymous via
// AllowAnonymous and out of antiforgery via DisableAntiforgery (ADR 0018).
var api = app.MapGroup("/api")
    .RequireAuthorization()
    .AddEndpointFilterFactory(
        (context, next) =>
        {
            var filter =
                context.ApplicationServices.GetRequiredService<AntiforgeryEndpointFilter>();
            return ctx => filter.InvokeAsync(ctx, next);
        }
    );

// Liveness / readiness probes and OpenAPI surfaces stay anonymous so probes from a
// reverse proxy / Scalar UI keep working when no user is logged in.
api.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
api.MapHealthChecks(
        "/healthz/ready",
        new HealthCheckOptions { Predicate = static c => c.Tags.Contains("readiness") }
    )
    .AllowAnonymous();

api.MapOpenApi().AllowAnonymous();
api.MapScalarApiReference(
        "/docs",
        options =>
        {
            options.WithOpenApiRoutePattern("/api/openapi/{documentName}.json");
            options.WithTitle("Balance API");
        }
    )
    .AllowAnonymous();

api.MapAuth();
api.MapAntiforgery();
api.MapAdminUsers();
api.MapAdminTokens();
api.MapCurrencies();
api.MapAccounts();
api.MapCounterparties();
api.MapBankAccounts();
api.MapBankTransactions();
api.MapJournalEntries();
api.MapDashboard();

await app.RunAsync(lifetime.ApplicationStopping);

internal partial class Program;
