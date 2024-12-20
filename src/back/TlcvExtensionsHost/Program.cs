using TlcvExtensionsHost.Configs;
using TlcvExtensionsHost.Services;
using TlcvExtensionsHost.Models;
using Microsoft.AspNetCore.Mvc;
using Serilog.Core;
using TlcvExtensionsHost;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

#pragma warning disable S1075 // URIs should not be hardcoded - FE script relies on this hardcoded port
builder.WebHost.UseUrls("http://127.0.0.1:5210");
#pragma warning restore S1075 // URIs should not be hardcoded

builder.Services.Configure<ServiceConfig>(builder.Configuration);
builder.Services.AddTlcvExtensions();
builder.Logging.AddSerilog(CreateLogger(builder.Configuration));

var app = builder.Build();

app.MapGet("/query",
    (EngineManager engineManager) => new QueryResponse(engineManager.GetCurrentInfos()));

app.MapPost("/fen",
    async ([FromBody] FenRequest request, EngineManager engineManager, ILogger<Program> logger) =>
    {
        logger.LogInformation("FEN: {Fen}", request.Fen);
        await engineManager.SetFenAsync(request.Fen);

        return new FenResponse(engineManager.Engines.ConvertAll(x => x.Config));
    });

app.Lifetime.ApplicationStarted.Register(() => app.Services.GetRequiredService<EngineManager>().Run());
app.Lifetime.ApplicationStopping.Register(() => StopEngineProcesses(app));

await app.RunAsync();

Console.WriteLine("Graceful shutdown completed");

static Logger CreateLogger(IConfiguration configuration)
{
    var loggerConfiguration = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .ReadFrom.Configuration(configuration);

    return loggerConfiguration.CreateLogger();
}

static void StopEngineProcesses(WebApplication app)
{
    var shutDownTasks = app.Services.GetRequiredService<EngineManager>().Engines.Select(e => e.ShutDown());

    Task.WhenAll(shutDownTasks).Wait();
}
