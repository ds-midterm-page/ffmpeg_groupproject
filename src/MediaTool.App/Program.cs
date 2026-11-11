using System.Diagnostics;
using MediaTool.App.Components;

const string AppUrl = "http://localhost:5080";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(AppUrl);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Open the user's browser once the server is listening (skipped under `dotnet run`, which uses launchSettings).
if (Environment.GetEnvironmentVariable("MEDIATOOL_NO_BROWSER") is null && !app.Environment.IsDevelopment())
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try { Process.Start(new ProcessStartInfo(AppUrl) { UseShellExecute = true }); }
        catch (Exception ex) { app.Logger.LogWarning(ex, "Could not open browser at {Url}", AppUrl); }
    });
}

app.Run();
