using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DDTank.LocalData;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
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
builder.Services.AddSingleton<IPasswordHasher<string>, PasswordHasher<string>>();
builder.Services.AddSingleton<NicknamePolicy>();
builder.Services.AddSingleton<GameLoginService>();
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
app.MapPost("/api/v1/accounts/register", (
    AccountRegistrationRequest request,
    CharacterCreationRepository characters,
    IPasswordHasher<string> passwordHasher) =>
{
    var userName = request.UserName?.Trim() ?? string.Empty;
    var password = request.Password ?? string.Empty;
    var email = request.Email?.Trim();

    if (!Regex.IsMatch(userName, @"^[\p{L}\p{N}_.-]{3,24}$"))
        return Results.BadRequest(new { error = "Username must be 3-24 letters, digits, dots, underscores, or hyphens." });
    if (password.Length < 8 || password.Length > 128)
        return Results.BadRequest(new { error = "Password must be between 8 and 128 characters." });
    if (email != null && email.Length > 254)
        return Results.BadRequest(new { error = "Email address is too long." });

    var passwordHash = passwordHasher.HashPassword(userName, password);
    try
    {
        var accountId = characters.CreateAccount(userName, passwordHash, email);
        return Results.Created("/api/v1/accounts/" + accountId, new
        {
            accountId,
            userName
        });
    }
    catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
    {
        return Results.Conflict(new { error = "An account with this username already exists." });
    }
});

app.MapPost("/api/v1/accounts/login", (
    AccountLoginRequest request,
    GameLoginService loginService) =>
{
    var userName = request.UserName?.Trim() ?? string.Empty;
    var password = request.Password ?? string.Empty;
    var result = loginService.Login(userName, password);
    if (result == null)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        accountId = result.AccountId,
        playerId = result.Player.PlayerId,
        gameTicket = result.GameTicket,
        expiresInSeconds = result.ExpiresInSeconds,
        requiresCharacterCreation = result.Player.RequiresCharacterCreation,
        nickname = result.Player.Nickname
    });
});

app.MapRazorPages();
app.MapMethods("/VisualizeRegister.ashx", new[] { "GET", "POST" }, HandleVisualizeRegisterAsync);
app.MapMethods("/Request/VisualizeRegister.ashx", new[] { "GET", "POST" }, HandleVisualizeRegisterAsync);

var localOptions = app.Services.GetRequiredService<
    Microsoft.Extensions.Options.IOptions<LocalWebOptions>>().Value;
app.Urls.Add(localOptions.BaseUrl);

app.Run();

static async Task<IResult> HandleVisualizeRegisterAsync(
    HttpRequest request,
    CharacterCreationRepository characters,
    NicknamePolicy nicknamePolicy,
    IConfiguration configuration)
{
    var form = request.HasFormContentType ? await request.ReadFormAsync() : null;
    string ReadValue(string key)
    {
        var formValue = form?[key].ToString();
        return !string.IsNullOrEmpty(formValue) ? formValue : request.Query[key].ToString();
    }

    var userName = ReadValue("Name").Trim();
    var gameTicket = ReadValue("Pass");
    var nickname = ReadValue("NickName");

    var account = characters.FindAccountByUserName(userName);
    if (account == null || !account.IsActive || string.IsNullOrEmpty(gameTicket))
        return VisualizeRegisterResponse(false, "Invalid game session.");

    var validation = nicknamePolicy.Validate(nickname);
    if (!validation.IsValid)
        return VisualizeRegisterResponse(false, validation.ErrorMessage ?? "Invalid nickname.");

    int? requestedSex = null;
    if (configuration.GetValue("CharacterCreation:MustChooseSex", true))
    {
        if (!bool.TryParse(ReadValue("Sex"), out var isMale))
            return VisualizeRegisterResponse(false, "A valid sex selection is required.");
        requestedSex = isMale ? 1 : 0;
    }

    var styleDate = configuration.GetValue("CharacterCreation:ValidDate", 3);
    var result = characters.CompleteCharacterCreation(
        account.AccountId, gameTicket, validation.Nickname, requestedSex, styleDate);

    return result.ResultCode switch
    {
        0 => VisualizeRegisterResponse(true, string.Empty),
        2 => VisualizeRegisterResponse(false, "Nickname already exists."),
        3 => VisualizeRegisterResponse(false, "Character is not awaiting creation."),
        _ => VisualizeRegisterResponse(false, "Character creation failed.")
    };
}

static IResult VisualizeRegisterResponse(bool success, string message)
{
    var xml = new XElement(
        "Result",
        new XAttribute("value", success ? "true" : "false"),
        new XAttribute("message", message));
    return Results.Content(xml.ToString(SaveOptions.DisableFormatting), "text/xml; charset=utf-8");
}

public sealed record AccountRegistrationRequest(string? UserName, string? Password, string? Email);
public sealed record AccountLoginRequest(string? UserName, string? Password);
