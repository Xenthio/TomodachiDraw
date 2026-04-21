using TomodachiDraw.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor Server
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Core services (all singletons — one Switch connection at a time)
builder.Services.AddSingleton<SwitchControllerService>();
builder.Services.AddSingleton<CanvasNavigatorService>();
builder.Services.AddSingleton<ImageProcessorService>();
builder.Services.AddSingleton<DrawOrchestrator>();

// Listen on all interfaces so you can reach from PC browser
builder.WebHost.UseUrls("http://0.0.0.0:5000");

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
