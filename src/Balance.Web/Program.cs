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
using Balance.Web.Endpoints.Imports;
using Balance.Web.Endpoints.JournalEntries;
using Balance.Web.Endpoints.JournalLines;
using Balance.Web.Endpoints.Loans;
using Balance.Web.Endpoints.Outlook;
using Balance.Web.Endpoints.Reports;
using Balance.Web.Endpoints.Search;
using Balance.Web.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Logging.AddConsole(builder.Environment);
builder.Services.AddBalanceServices(builder.Configuration, builder.Environment);
builder.Services.AddBalanceIntegrationIng();
builder.Services.AddBalanceWeb(builder.Configuration, builder.Environment);

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

// Antiforgery is a browser-only concern. The built-in middleware doesn't differentiate
// by auth scheme, so wrap it in UseWhen to skip when the request authenticated via a
// PAT (no ambient credential = no CSRF surface).
app.UseWhen(
    ctx =>
        !string.Equals(
            ctx.User.Identity?.AuthenticationType,
            AuthSchemes.ApiToken,
            StringComparison.Ordinal
        ),
    branch => branch.UseAntiforgery()
);

// Serve Balance.Web.Client SPA
app.MapStaticAssets();
app.MapFallbackToFile("index.html");

// Prefix all Balance.Web endpoints with /api. The group requires authentication and
// stamps every endpoint with AutoValidateAntiforgeryTokenAttribute so the built-in
// UseAntiforgery() middleware enforces XSRF on unsafe HTTP methods. Individual endpoints
// opt back via AllowAnonymous and DisableAntiforgery (ADR 0016).
var api = app.MapGroup("/api")
    .RequireAuthorization()
    .WithMetadata(new AutoValidateAntiforgeryTokenAttribute());

// Unmatched /api/* URLs must be API 404s (Problem Details via the status-code pages
// middleware), never the SPA shell: this group fallback is more specific than the
// MapFallbackToFile catch-all above, so it wins route precedence.
api.MapFallback(static () => Results.NotFound()).AllowAnonymous().DisableAntiforgery();

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
api.MapImports();
api.MapBankTransactions();
api.MapJournalEntries();
api.MapJournalLines();
api.MapLoans();
api.MapOutlook();
api.MapDashboard();
api.MapReports();
api.MapSearch();

await app.RunAsync(lifetime.ApplicationStopping);

internal partial class Program;
