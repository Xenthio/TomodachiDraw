using TomodachiDraw.Models;

namespace TomodachiDraw.Services;

/// <summary>
/// Debug interface for testing individual Canvas API operations.
/// Exposed via the Blazor debug panel for manual testing.
/// </summary>
public class DebugController(CanvasAPI api)
{
    public string LastResult { get; private set; } = "Ready.";
    public string ModeDisplay => api.CurrentMode.ToString();
    public bool IsSafeMode => api.CurrentMode == CanvasAPI.Mode.Safe;

    // =========================================================================
    // Mode toggles
    // =========================================================================

    public void SetSafeMode()
    {
        api.CurrentMode = CanvasAPI.Mode.Safe;
        LastResult = "Switched to Safe mode (full realignment before each op)";
    }

    public void SetSpeedMode()
    {
        api.CurrentMode = CanvasAPI.Mode.Speed;
        LastResult = "Switched to Speed mode (local navigation, trust state)";
    }

    public void ToggleMode()
    {
        if (IsSafeMode) SetSpeedMode();
        else SetSafeMode();
    }

    // =========================================================================
    // Tool operations
    // =========================================================================

    public async Task SetToolPencilAsync()
    {
        try
        {
            await api.SetToolAsync(CanvasNavigatorService.Tool.Pencil);
            LastResult = "✓ Tool set to Pencil";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    public async Task SetToolFillAsync()
    {
        try
        {
            await api.SetToolAsync(CanvasNavigatorService.Tool.Fill);
            LastResult = "✓ Tool set to Fill";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    public async Task SetToolEraserAsync()
    {
        try
        {
            await api.SetToolAsync(CanvasNavigatorService.Tool.Eraser);
            LastResult = "✓ Tool set to Eraser";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    // =========================================================================
    // Palette operations
    // =========================================================================

    public async Task ActivatePaletteSlot0Async()
    {
        try
        {
            await api.ActivatePaletteSlotAsync(0);
            LastResult = "✓ Palette slot 0 activated";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    public async Task ActivatePaletteSlot1Async()
    {
        try
        {
            await api.ActivatePaletteSlotAsync(1);
            LastResult = "✓ Palette slot 1 activated";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    public async Task SetPaletteSlot0RedAsync()
    {
        try
        {
            await api.SetPaletteColourAsync(0, new DrawColour { Hue = 0, Saturation = 1, Brightness = 1 });
            LastResult = "✓ Palette slot 0 set to Red";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    public async Task SetPaletteSlot0BlackAsync()
    {
        try
        {
            await api.SetPaletteColourAsync(0, new DrawColour { Hue = 0, Saturation = 0, Brightness = 0 });
            LastResult = "✓ Palette slot 0 set to Black";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    // =========================================================================
    // Navigation
    // =========================================================================

    public async Task NavigateToCentreAsync()
    {
        try
        {
            await api.NavigateToAsync(128, 128);
            LastResult = "✓ Navigated to centre (128, 128)";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    public async Task NavigateToOriginAsync()
    {
        try
        {
            await api.NavigateToAsync(0, 0);
            LastResult = "✓ Navigated to origin (0, 0)";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    public async Task NavigateToCornerAsync()
    {
        try
        {
            await api.NavigateToAsync(255, 255);
            LastResult = "✓ Navigated to corner (255, 255)";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    // =========================================================================
    // Drawing
    // =========================================================================

    public async Task DrawPixelAsync()
    {
        try
        {
            await api.DrawPixelAsync();
            LastResult = "✓ Drew pixel";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    public async Task FillAsync()
    {
        try
        {
            await api.FillAsync();
            LastResult = "✓ Filled region";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    // =========================================================================
    // State
    // =========================================================================

    public async Task RealignAsync()
    {
        try
        {
            await api.RealignAsync();
            LastResult = "✓ Realigned (homed to 128,128, reset state)";
        }
        catch (Exception ex) { LastResult = $"✗ Error: {ex.Message}"; }
    }

    public string GetStateInfo()
    {
        var (x, y, tool, slot, mode) = api.GetState();
        return $"Pos({x},{y}) Tool={tool} Slot={slot} Mode={mode}";
    }
}
