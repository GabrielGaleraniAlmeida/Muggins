using Muggins.Engine.Api;
using Muggins.Engine.Bot;
using Muggins.Web;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, ".muggins-data");
var keyDirectory = Path.Combine(dataDirectory, "keys");
Directory.CreateDirectory(keyDirectory);
if (!OperatingSystem.IsWindows())
{
    File.SetUnixFileMode(dataDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    File.SetUnixFileMode(keyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
    .SetApplicationName("Muggins.LocalWeb");
builder.Services.AddSingleton(provider => new EncryptedSessionStore(
    dataDirectory,
    provider.GetRequiredService<IDataProtectionProvider>()));
builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(20) });
builder.Services.AddSingleton<MugginsApiClient>();
builder.Services.AddSingleton<MugginsBot>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/app-api") &&
        context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
    {
        var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
        if (string.Equals(fetchSite, "cross-site", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Requisição de outra origem bloqueada." });
            return;
        }

        var origin = context.Request.Headers["Origin"].ToString();
        if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri) &&
            !string.Equals(originUri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Origem da requisição não autorizada." });
            return;
        }
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapMugginsWebEndpoints();
app.MapFallbackToFile("index.html");

app.Run();
