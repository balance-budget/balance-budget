using Balance.Data.Helpers;
using Balance.Services;
using Balance.Web;
using Balance.Web.Endpoints;
using Balance.Web.Endpoints.Currencies;
using Balance.Web.Helpers;
using Balance.Web.Logging;
using Balance.Web.Middleware;
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

app.MapHealthChecks("/healthz");
app.MapStaticAssets();
app.MapHtmx();
app.MapOpenApi();
app.MapScalarApiReference();

// Middleware pipeline, order matters here
app.UseForwardedHeaders();
app.UseDefaultFiles();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseMiddleware<DomainExceptionMiddleware>();

app.MapCurrencies();

await app.RunAsync(lifetime.ApplicationStopping);

internal partial class Program;
