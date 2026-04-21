using TomodachiDraw.Models;

namespace TomodachiDraw.Services;

/// <summary>
/// High-level navigation for the Tomodachi Life 2 drawing canvas.
///
/// Design principles:
///   - Every public method is a named action that reads like English
///   - State is tracked so we navigate locally when possible
///   - All timing constants are at the top for easy tuning
///   - Private helpers do the raw button sequences
///
/// Canvas calibration (Xenthio's measurements):
///   Hold UP+LEFT → screen edge → DOWN 76 → RIGHT 192 → canvas (0,0)
///
/// Toolbar layout (from RIGHT anchor = Settings):
///   0=Settings  1=Filters  2=Eyedrop  3=Eraser  4=Pencil  5=Fill
///   6=Shape  7=Stamp  8=Text  9=Select  10=Move
///   (Pencil confirmed at 3 from right — eraser was one too many)
///
/// Colour picker (Xenthio's measurements):
///   Y→sidebar  Y→edit slot  R→HSB picker
///   Rect: 212 wide (left=white, right=full sat), 111 tall (top=bright, bottom=black)
///   Hue: ZL/ZR, 200 steps
///   A=confirm
///
/// Brush setup:
///   Hover pencil in toolbar → X → opens pencil properties
///   UP+LEFT to anchor → A = select Hard Edge brush type
///   X twice to reopen → UP+LEFT → DOWN once = circle row → A = 1x1 circle
///
/// Button names: up/down/left/right (NOT d_up etc.)
/// </summary>
public class CanvasNavigatorService(SwitchControllerService controller)
{
    // =========================================================================
    // Timing constants (ms) — tune these if inputs are missed or doubled
    // =========================================================================

    /// <summary>Hold time for anchoring (slam to edge). Must be long enough for any menu width.</summary>
    private const int AnchorHoldMs = 5000;

    /// <summary>Settle time after releasing an anchor hold.</summary>
    private const int AnchorSettleMs = 300;

    /// <summary>Hold time for each individual dpad step press.</summary>
    private const int StepHoldMs = 60;   // hold per dpad press
    /// <summary>Settle time between individual dpad step presses (canvas navigation).</summary>
    private const int StepSettleMs = 50;  // was 100ms — reduced for speed

    /// <summary>Settle time between dpad steps inside menus/colour picker (faster, smaller menus).</summary>
    private const int MenuStepMs = 60;

    /// <summary>Settle time after opening a menu (Y, X, R buttons).</summary>
    private const int MenuOpenMs = 300;

    /// <summary>Settle time after pressing A to confirm.</summary>
    private const int ConfirmMs = 250;

    // =========================================================================
    // Canvas calibration
    // =========================================================================

    // Canvas centre position after tool-select trick
    private const int CanvasCentreX = 128;
    private const int CanvasCentreY = 128;

    // Draw speed — mutable so UI can tune without redeploying
    /// <summary>Milliseconds to settle between canvas dpad steps. Lower = faster drawing.</summary>
    public static int DrawStepSettleMs = 50;

    // =========================================================================
    // Toolbar positions (steps LEFT from Settings anchor)
    // =========================================================================

    // Toolbar steps LEFT from Settings anchor (confirmed by Xenthio):
    // Settings=0, ?, ?, Eraser=4, Pencil=5, Fill=6, Shape=7, Stamp=8, Text=9, Select=10, Move=11
    public enum Tool { Settings = 0, Eraser = 4, Pencil = 5, Fill = 6, Shape = 7, Stamp = 8, Text = 9, Select = 10, Move = 11 }

    // =========================================================================
    // Colour picker dimensions
    // =========================================================================

    private const int PaletteSidebarSlots = 9;

    // Colour picker calibration — public so UI can tune without redeploying
    public static int ColourRectWidth  = 212; // steps left=white to right=full sat
    public static int ColourRectHeight = 111; // steps top=bright to bottom=black
    public static int HueSliderSteps   = 200; // ZL/ZR total steps

    // =========================================================================
    // Tracked state
    // =========================================================================

    /// <summary>Current canvas cursor position. (-1,-1) = not homed.</summary>
    public int CursorX { get; private set; } = -1;
    public int CursorY { get; private set; } = -1;

    /// <summary>Which palette slot is currently active.</summary>
    public int ActivePaletteSlot { get; private set; } = 0;

    // =========================================================================
    // Setup sequence (call once before drawing)
    // =========================================================================

    /// <summary>
    /// Full setup: ensure canvas focus → select pencil → set 1x1 hard-edge brush → home cursor.
    /// </summary>
    public async Task SetupAsync()
    {
        await EnsureCanvasFocusAsync();
        await SelectToolAsync(Tool.Pencil);
        await SetBrush1x1HardEdgeAsync();
        await HomeCursorAsync();
    }

    // =========================================================================
    // Canvas focus
    // =========================================================================

    /// <summary>Press B to dismiss any open menus and return focus to canvas.</summary>
    public async Task EnsureCanvasFocusAsync()
    {
        await controller.PressAsync("b");
        await Task.Delay(300);
    }

    // =========================================================================
    // Cursor homing
    // =========================================================================

    /// <summary>Anchor to origin and return to canvas ready to draw.</summary>
    public async Task HomeCursorAsync()
    {
        // Start from canvas centre (128,128) — no need to home to (0,0)
        // We'll navigate relatively from here to wherever we need to draw
        CursorX = 128;
        CursorY = 128;
        UpdateControllerState();
    }

    // =========================================================================
    // Cursor movement
    // =========================================================================

    /// <summary>Move canvas cursor to an absolute coordinate.</summary>
    public async Task MoveToAsync(int x, int y)
    {
        if (CursorX < 0) throw new InvalidOperationException("Must call HomeCursorAsync() first.");

        int dx = x - CursorX;
        int dy = y - CursorY;

        if (dx > 0) await controller.DpadAsync("right", dx,  delayMs: DrawStepSettleMs);
        else if (dx < 0) await controller.DpadAsync("left", -dx, delayMs: DrawStepSettleMs);

        if (dy > 0) await controller.DpadAsync("down",  dy,  delayMs: DrawStepSettleMs);
        else if (dy < 0) await controller.DpadAsync("up",  -dy, delayMs: DrawStepSettleMs);

        CursorX = x;
        CursorY = y;
        UpdateControllerState();
    }

    // =========================================================================
    // Drawing
    // =========================================================================

    public async Task DrawPixelAsync() => await controller.PressAsync("a");
    public async Task FillAsync()      => await controller.PressAsync("a");

    // =========================================================================
    // Tool selection
    // =========================================================================

    /// <summary>
    /// Select a tool from the toolbar.
    /// Opens toolbar (X), anchors right to Settings, steps left to target.
    /// </summary>
    public async Task SelectToolAsync(Tool tool)
    {
        await controller.PressAsync("x");  // open toolbar
        await Task.Delay(MenuOpenMs);

        await AnchorRightAsync();           // slam to Settings

        int steps = (int)tool;
        if (steps > 0)
            await controller.DpadAsync("left", steps, delayMs: MenuStepMs);

        await controller.PressAsync("a");  // select
        await Task.Delay(ConfirmMs);
    }

    // =========================================================================
    // Brush setup
    // =========================================================================

    /// <summary>
    /// Sets pencil to 1x1 hard-edge circle brush.
    /// Assumes pencil tool is already selected (cursor is on pencil in toolbar).
    ///
    /// Step 1: Hover pencil in toolbar → press X → properties panel opens
    ///   → anchor top-left → A = select Hard Edge type
    /// Step 2: X twice to reopen → anchor top-left → DOWN once (circle row) → A = 1x1
    /// </summary>
    public async Task SetBrush1x1HardEdgeAsync()
    {
        // Brush setup via menus is fragile. Skip for now.
        // TODO: Re-enable once menu navigation is bulletproof
        await Task.Delay(100);
    }

    // =========================================================================
    // Colour palette management
    // =========================================================================

    /// <summary>
    /// Set palette slot colour and activate it.
    /// Y (sidebar) → navigate to slot → Y (edit) → R (HSB picker) → set hue → set SB → A → B (canvas)
    /// </summary>
    public async Task SetPaletteSlotColourAsync(int slotIndex, DrawColour colour)
    {
        await controller.PressAsync("y");  // open sidebar
        await Task.Delay(MenuOpenMs);

        await NavigateToSlotAsync(slotIndex);

        await controller.PressAsync("y");  // edit this slot
        await Task.Delay(MenuOpenMs);

        await controller.PressAsync("r");  // open HSB picker
        await Task.Delay(MenuOpenMs);

        await SetHueAsync(colour.Hue);
        await SetColourRectAsync(
            (int)(colour.Saturation * ColourRectWidth),
            (int)((1f - colour.Brightness) * ColourRectHeight));

        await controller.PressAsync("a");  // confirm
        await Task.Delay(ConfirmMs);

        await EnsureCanvasFocusAsync();
        ActivePaletteSlot = slotIndex;
    }

    /// <summary>Switch to a palette slot without changing its colour.</summary>
    public async Task ActivatePaletteSlotAsync(int slotIndex)
    {
        await controller.PressAsync("y");
        await Task.Delay(MenuOpenMs);
        await NavigateToSlotAsync(slotIndex);
        await controller.PressAsync("a");
        await Task.Delay(150);
        await EnsureCanvasFocusAsync();
        ActivePaletteSlot = slotIndex;
    }

    // =========================================================================
    // Anchor helpers
    // =========================================================================

    /// <summary>
    /// Slam cursor to top-left using left analog stick (fast). Use for colour picker rect.
    /// </summary>
    public async Task AnchorTopLeftAsync()
    {
        await controller.StickAsync("upleft");
        await Task.Delay(AnchorHoldMs);
        await controller.StickAsync("center");
        await Task.Delay(AnchorSettleMs);
    }

    /// <summary>
    /// Slam selection to top-left using dpad hold. Use for menus (brush properties etc)
    /// that may not respond to analog stick.
    /// </summary>
    private async Task AnchorTopLeftDpadAsync()
    {
        await controller.HoldAsync("up+left");
        try { await Task.Delay(AnchorHoldMs); }
        finally
        {
            await controller.ReleaseAsync();
            await Task.Delay(AnchorSettleMs);
        }
    }

    /// <summary>Hold RIGHT to slam selection to the rightmost item.</summary>
    private async Task AnchorRightAsync()
    {
        await controller.HoldAsync("right");
        try { await Task.Delay(AnchorHoldMs); }
        finally
        {
            await controller.ReleaseAsync();
            await Task.Delay(AnchorSettleMs);
        }
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>Navigate sidebar to a slot. Anchors to bottom (slot 8) then steps up.</summary>
    private async Task NavigateToSlotAsync(int slotIndex)
    {
        await controller.DpadHoldAsync("down", AnchorHoldMs);
        await Task.Delay(AnchorSettleMs);
        int stepsUp = (PaletteSidebarSlots - 1) - slotIndex;
        if (stepsUp > 0)
            await controller.DpadAsync("up", stepsUp, delayMs: MenuStepMs);
    }

    /// <summary>Set hue via ZL reset (to 0) then ZR steps to target.</summary>
    private async Task SetHueAsync(float hue)
    {
        int targetStep = (int)(hue / 360f * HueSliderSteps);

        await controller.HoldAsync("zl");
        await Task.Delay(HueSliderSteps * 40 + 500);
        await controller.ReleaseAsync();
        await Task.Delay(150);

        for (int i = 0; i < targetStep; i++)
        {
            await controller.PressAsync("zr", holdMs: 40);
            await Task.Delay(30);
        }
    }

    /// <summary>Position the 2D colour rect. Anchors to top-left then steps to target.</summary>
    private async Task SetColourRectAsync(int satX, int briY)
    {
        await AnchorTopLeftAsync();
        if (satX > 0) await controller.DpadAsync("right", satX, delayMs: 40);
        if (briY > 0) await controller.DpadAsync("down",  briY,  delayMs: 40);
    }

    private void UpdateControllerState()
    {
        controller.State.CursorX = CursorX;
        controller.State.CursorY = CursorY;
    }
}
