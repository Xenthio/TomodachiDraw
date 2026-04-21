namespace TomodachiDraw.Models;

using TomodachiDraw.Services;

/// <summary>
/// A single executable command in the draw sequence.
/// These are atomic, deterministic operations that don't require state tracking.
/// </summary>
public abstract record DrawCommand;

/// <summary>Ensure we're on the canvas (press B if in a menu).</summary>
public record EnsureCanvasFocus : DrawCommand;

/// <summary>Anchor to a known position via deterministic sequence.</summary>
public record AnchorToOrigin : DrawCommand;

/// <summary>Set a specific palette slot to a specific colour.</summary>
public record SetPaletteSlot(int Slot, DrawColour Colour) : DrawCommand;

/// <summary>Activate a palette slot (make it the current drawing colour).</summary>
public record ActivateSlot(int Slot) : DrawCommand;

/// <summary>Navigate on canvas to (X, Y) from current position.</summary>
public record NavigateTo(int X, int Y) : DrawCommand;

/// <summary>Draw a single pixel at current cursor position.</summary>
public record DrawPixel : DrawCommand;

/// <summary>Fill the region at current cursor position.</summary>
public record FillRegion : DrawCommand;

/// <summary>Select a tool.</summary>
public record SelectTool(CanvasNavigatorService.Tool Tool) : DrawCommand;

/// <summary>Wait (useful for settling after input sequences).</summary>
public record Wait(int Ms) : DrawCommand;
