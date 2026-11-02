using System.Net;
using DDTank.Web.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var configuredDataDirectory = builder.Configuration["LocalData:Directory"];
var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
if (string.IsNullOrWhiteSpace(localApplicationData) && string.IsNullOrWhiteSpace(configuredDataDirectory))
    throw new InvalidOperationException("The current user does not have a LocalApplicationData directory.");

var dataDirectory = string.IsNullOrWhiteSpace(configuredDataDirectory)
    ? Path.Combine(localApplicationData, "DDTank", "data")
    : Path.GetFullPath(configuredDataDirectory);
var referenceSeedPath = Path.Combine(AppContext.BaseDirectory, "Seeds", "character-reference-seed.sqlite3");
var localDatabase = new LocalDatabaseRuntime(dataDirectory, referenceSeedPath);

builder.Services.AddSingleton(localDatabase);
builder.Services.AddSingleton(localDatabase.Database);
builder.Services.AddSingleton(localDatabase.Characters);
builder.Services.AddProblemDetails();
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks()
    .AddCheck<LocalDatabaseHealthCheck>("local-sqlite-database");
builder.Services.AddOptions<LocalWebOptions>()
    .Bind(builder.Configuration.GetSection(LocalWebOptions.SectionName))
    .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address))),
        "The local web host must bind to an HTTP loopback address.")
    .ValidateOnStart();

var app = builder.Build();

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.XFrameOptions = "SAMEORIGIN";
    await next();
});

app.UseStaticFiles();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");
app.MapGet("/api/v1/server/info", (LocalDatabaseRuntime database) =>
{
    var ready = database.IsReady(out var description);
    return Results.Ok(new
    {
        name = "DDTank",
        edition = "Windows Portable",
        web = "ASP.NET Core",
        database = "SQLite",
        databaseReady = ready,
        databaseStatus = description,
        gameRuntime = "not-connected"
    });
});
app.MapRazorPages();

var localOptions = app.Services.GetRequiredService<
    Microsoft.Extensions.Options.IOptions<LocalWebOptions>>().Value;
app.Urls.Add(localOptions.BaseUrl);

app.Run();
