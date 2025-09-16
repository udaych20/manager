using systems_manager.src;

var builder = WebApplication.CreateBuilder(args);
ConfigPropertiesReader configPropertiesReader = new(); // No file path needed, reads env vars

// Configure logging
builder.Logging.ClearProviders(); // Optional: Clears all the default logging providers
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Use the new environment variable name for log level
if (Enum.TryParse<LogLevel>(configPropertiesReader.GetPropertyValue("SM_APP_LOG_LEVEL"), true, out var logLevel))
{
    builder.Logging.SetMinimumLevel(logLevel);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
});


Console.WriteLine("Application Started...");
var app = builder.Build();

/*app.MapGet("/log", (ILogger<Program> logger) =>
{
    logger.LogInformation("Logging from the /log at {Time}", DateTime.UtcNow);
    return "Check the logs!";
});*/

app.MapGet("/health", () => "I'M Alive!");

SystemsManager.Initialize(app.Services.GetService<ILoggerFactory>(), app, configPropertiesReader);

app.Run();

// Ensure to flush the log buffer on application shutdown
//Log.CloseAndFlush();