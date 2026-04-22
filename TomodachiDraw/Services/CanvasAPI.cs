using TomodachiDraw.Models;

namespace TomodachiDraw.Services;

/// <summary>
/// High-level Canvas API — the *intended* interface for drawing.
/// Hides all input details. Can toggle between Safe (realign always) and Speed (trust state) modes.
///
/// Usage:
///   var api = new CanvasAPI(controller, navigator);
///   await api.SetToolAsync(Tool.Pencil);           // abstracted
///   await api.SetPaletteSlotAsync(0);              // abstracted
///   await api.SetPaletteColourAsync(0, Color.Red); // abstracted
///   await api.NavigateToAsync(100, 50);            // abstracted
///   await api.DrawPixelAsync();                    // abstracted
/// </summary>
public class CanvasAPI
{
    private readonly SwitchControllerService _controller;
    private readonly CanvasNavigatorService _navigator;

    public enum Mode { Safe, Speed }
    public Mode CurrentMode { get; set; } = Mode.Speed;

    /// <summary>Track state for Speed mode. Can be reset via Safe mode.</summary>
    private int _trackedX = 128;
    private int _trackedY = 128;
    private CanvasNavigatorService.Tool _trackedTool = CanvasNavigatorService.Tool.Pencil;
    private int _trackedPaletteSlot = 0;

    public CanvasAPI(SwitchControllerService controller, CanvasNavigatorService navigator)
    {
        _controller = controller;
        _navigator = navigator;
    }

    // =========================================================================
    // High-level drawing operations (implementation hidden)
    // =========================================================================

    /// <summary>Set the active tool (Safe: full anchor + reselect. Speed: local nav if possible).</summary>
    public async Task SetToolAsync(CanvasNavigatorService.Tool tool)
    {
        if (CurrentMode == Mode.Safe)
        {
            // Full realignment: ensure canvas focus, re-anchor, navigate to tool
            await _navigator.EnsureCanvasFocusAsync();
            await _navigator.HomeCursorAsync();
            await _navigator.SelectToolAsync(tool);
            _trackedTool = tool;
            _trackedX = 128;
            _trackedY = 128;
        }
        else
        {
            // Speed mode: navigate locally from current tool
            if (tool != _trackedTool)
            {
                await _navigator.SelectToolAsync(tool);
                _trackedTool = tool;
            }
        }
    }

    /// <summary>Set a palette slot (without changing colour). Safe: full anchor. Speed: local nav.</summary>
    public async Task ActivatePaletteSlotAsync(int slot)
    {
        if (CurrentMode == Mode.Safe)
        {
            await _navigator.EnsureCanvasFocusAsync();
            await _navigator.ActivatePaletteSlotAsync(slot);
            _trackedPaletteSlot = slot;
        }
        else
        {
            if (slot != _trackedPaletteSlot)
            {
                await _navigator.ActivatePaletteSlotAsync(slot);
                _trackedPaletteSlot = slot;
            }
        }
    }

    /// <summary>Set a palette slot's colour (opens sidebar, navigates, picks colour).</summary>
    public async Task SetPaletteColourAsync(int slot, DrawColour colour)
    {
        await _navigator.SetPaletteSlotColourAsync(slot, colour);
        _trackedPaletteSlot = slot;

        if (CurrentMode == Mode.Speed)
        {
            // After colour picker, we're back on canvas
            _trackedX = 128;
            _trackedY = 128;
        }
    }

    /// <summary>Navigate to a canvas position. Safe: full home first. Speed: relative delta (auto-init if needed).</summary>
    public async Task NavigateToAsync(int x, int y)
    {
        // Auto-initialize if cursor was never homed (Speed mode + first navigation)
        if (_navigator.CursorX < 0 || CurrentMode == Mode.Safe)
        {
            await _navigator.EnsureCanvasFocusAsync();
            await _navigator.HomeCursorAsync();
            _trackedX = 0;
            _trackedY = 0;
        }

        // Navigate relatively from tracked position
        await _navigator.MoveToAsync(x, y);
        _trackedX = x;
        _trackedY = y;
    }

    /// <summary>Draw a single pixel at current position.</summary>
    public async Task DrawPixelAsync()
    {
        await _navigator.DrawPixelAsync();
    }

    /// <summary>Fill the region at current position.</summary>
    public async Task FillAsync()
    {
        await _navigator.FillAsync();
    }

    // =========================================================================
    // State management
    // =========================================================================

    /// <summary>Force realignment (Safe mode: know position, Speed mode: learn position).</summary>
    public async Task RealignAsync()
    {
        await _navigator.EnsureCanvasFocusAsync();
        await _navigator.HomeCursorAsync();
        _trackedX = 128;
        _trackedY = 128;
        _trackedTool = CanvasNavigatorService.Tool.Pencil;
        _trackedPaletteSlot = 0;
    }

    /// <summary>Get current tracked state (for debugging).</summary>
    public (int X, int Y, CanvasNavigatorService.Tool Tool, int Slot, Mode Mode) GetState()
        => (_trackedX, _trackedY, _trackedTool, _trackedPaletteSlot, CurrentMode);
}
