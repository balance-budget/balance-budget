using Balance.Data.Helpers;
using Balance.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole();
builder.Services.AddBalanceServices(builder.Configuration, false);

var app = builder.Build();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
await app.MigrateDatabase(lifetime.ApplicationStopping);

var schedulerFactory = app.Services.GetRequiredService<ISchedulerFactory>();
var scheduler = await schedulerFactory.GetScheduler();

await app.RunAsync(lifetime.ApplicationStopping);
