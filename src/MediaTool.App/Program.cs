using System.Diagnostics;
using MediaTool.App;
using MediaTool.App.Components;
using MediaTool.Media;

const string AppUrl = "http://localhost:5080";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(AppUrl);

builder.Services.Configure<YtDlpOptions>(builder.Configuration.GetSection(YtDlpOptions.SectionName));
builder.Services.AddSingleton<IYtDlpClient, YtDlpProcessClient>();
builder.Services.AddSingleton<MetadataService>();
builder.Services.Configure<FfmpegOptions>(builder.Configuration.GetSection(FfmpegOptions.SectionName));
builder.Services.AddSingleton<IFfmpegRunner, FfmpegRunner>();
builder.Services.AddSingleton<ITempFileManager>(new TempFileManager(TempFileManager.DefaultRoot));
builder.Services.AddSingleton<JobStore>();
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddSingleton<DownloadJobRunner>();
builder.Services.AddHostedService<DownloadWorker>();

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
app.MapFileEndpoints();
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
