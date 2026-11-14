using System.Diagnostics;
using MediaTool.App;
using MediaTool.App.Components;
using MediaTool.Core.Hosting;
using MediaTool.Media;

var builder = WebApplication.CreateBuilder(args);

// Desktop: localhost only. Docker sets ASPNETCORE_URLS=http://+:5080 so the port can be published.
builder.WebHost.UseUrls(HostingSettings.ResolveUrls(builder.Configuration["urls"]));

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
builder.Services.Configure<StickerOptions>(builder.Configuration.GetSection("Stickers"));
builder.Services.AddSingleton<StickerConverter>();
builder.Services.AddHostedService<StickerCleanupWorker>();

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
app.MapGet("/stickers/files/{id:guid}", (Guid id, bool? download, StickerConverter converter) =>
{
    var result = converter.Get(id);
    if (result is null || !File.Exists(result.Path)) return Results.NotFound("This sticker has expired. Please convert it again.");
    if (result.Created < DateTime.UtcNow.AddHours(-1))
    {
        if (converter.Take(id) is { } expired) StickerConverter.Delete(expired);
        return Results.NotFound("This sticker has expired. Please convert it again.");
    }
    var mime = result.Extension switch { "png" => "image/png", "webp" => "image/webp", _ => "video/webm" };
    return Results.File(result.Path, mime, download == true ? "sticker." + result.Extension : null, enableRangeProcessing: true);
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Open the user's browser once the server is listening (desktop exe only).
if (HostingSettings.ShouldOpenBrowser(
        app.Environment.IsDevelopment(),
        Environment.GetEnvironmentVariable("MEDIATOOL_NO_BROWSER"),
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try { Process.Start(new ProcessStartInfo(HostingSettings.DefaultUrl) { UseShellExecute = true }); }
        catch (Exception ex) { app.Logger.LogWarning(ex, "Could not open browser at {Url}", HostingSettings.DefaultUrl); }
    });
}

app.Run();
