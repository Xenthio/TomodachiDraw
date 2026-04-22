using TomodachiDraw.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor Server
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Core services (all singletons — one Switch connection at a time)
builder.Services.AddSingleton<SwitchControllerService>();
builder.Services.AddSingleton<CanvasNavigatorService>();
builder.Services.AddSingleton<ImageProcessorService>();
builder.Services.AddSingleton<DrawOrchestrator>();
builder.Services.AddSingleton<CanvasAPI>();
builder.Services.AddSingleton<DebugController>();

// Listen on all interfaces so you can reach from PC browser
builder.WebHost.UseUrls("http://0.0.0.0:5000");

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

// Debug API endpoint
app.MapPost("/api/debug", async (HttpContext ctx, DebugController debug) =>
{
    using var reader = new System.IO.StreamReader(ctx.Request.Body);
    var json = await reader.ReadToEndAsync();
    var req = System.Text.Json.JsonSerializer.Deserialize<DebugRequest>(json);
    
    if (req?.Action == "SetSafeMode") debug.SetSafeMode();
    else if (req?.Action == "SetSpeedMode") debug.SetSpeedMode();
    else if (req?.Action == "SetToolPencil") await debug.SetToolPencilAsync();
    else if (req?.Action == "SetToolFill") await debug.SetToolFillAsync();
    else if (req?.Action == "SetToolEraser") await debug.SetToolEraserAsync();
    else if (req?.Action == "Slot0") await debug.ActivatePaletteSlot0Async();
    else if (req?.Action == "Slot1") await debug.ActivatePaletteSlot1Async();
    else if (req?.Action == "Slot0Red") await debug.SetPaletteSlot0RedAsync();
    else if (req?.Action == "Slot0Black") await debug.SetPaletteSlot0BlackAsync();
    else if (req?.Action == "Centre") await debug.NavigateToCentreAsync();
    else if (req?.Action == "Origin") await debug.NavigateToOriginAsync();
    else if (req?.Action == "Corner") await debug.NavigateToCornerAsync();
    else if (req?.Action == "Pixel") await debug.DrawPixelAsync();
    else if (req?.Action == "Fill") await debug.FillAsync();
    else if (req?.Action == "Realign") await debug.RealignAsync();
    
    var resp = new DebugResponse 
    { 
        Mode = debug.ModeDisplay, 
        Result = debug.LastResult, 
        State = debug.GetStateInfo() 
    };
    
    await ctx.Response.WriteAsJsonAsync(resp);
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

// DTOs for debug API
public class DebugRequest
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }
}

public class DebugResponse
{
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }
    
    [JsonPropertyName("result")]
    public string? Result { get; set; }
    
    [JsonPropertyName("state")]
    public string? State { get; set; }
}
