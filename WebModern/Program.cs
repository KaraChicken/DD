using System.Net;
using DDTank.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();
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
app.MapHealthChecks("/health/live");
app.MapGet("/health/ready", () => Results.Ok(new
{
    status = "ready",
    component = "web-host",
    note = "Database and game-runtime readiness checks are not wired yet."
}));
app.MapGet("/api/v1/server/info", () => Results.Ok(new
{
    name = "DDTank",
    edition = "Windows Portable",
    web = "ASP.NET Core",
    gameRuntime = "not-connected"
}));
app.MapRazorPages();

var localOptions = app.Services.GetRequiredService<
    Microsoft.Extensions.Options.IOptions<LocalWebOptions>>().Value;
app.Urls.Add(localOptions.BaseUrl);

app.Run();
