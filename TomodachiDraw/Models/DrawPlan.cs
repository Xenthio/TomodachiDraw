namespace TomodachiDraw.Models;

/// <summary>
/// Represents a single colour in the draw plan.
/// Colours are stored as HSB (hue 0-360, saturation 0-1, brightness 0-1)
/// because the in-game colour picker uses HSB — no conversion needed at draw time.
/// </summary>
public record DrawColour(float Hue, float Saturation, float Brightness)
{
    /// <summary>Convert from RGB (0-255 each) to HSB.</summary>
    public static DrawColour FromRgb(byte r, byte g, byte b)
    {
        float rf = r / 255f, gf = g / 255f, bf = b / 255f;
        float max = Math.Max(rf, Math.Max(gf, bf));
        float min = Math.Min(rf, Math.Min(gf, bf));
        float delta = max - min;

        float hue = 0;
        if (delta > 0)
        {
            if (max == rf) hue = 60 * (((gf - bf) / delta) % 6);
            else if (max == gf) hue = 60 * (((bf - rf) / delta) + 2);
            else hue = 60 * (((rf - gf) / delta) + 4);
        }
        if (hue < 0) hue += 360;

        float sat = max == 0 ? 0 : delta / max;
        return new DrawColour(hue, sat, max);
    }

    /// <summary>Approximate perceptual distance between two colours (HSB space).</summary>
    public float DistanceTo(DrawColour other)
    {
        float dh = Math.Abs(Hue - other.Hue);
        if (dh > 180) dh = 360 - dh;
        return (dh / 180f) + Math.Abs(Saturation - other.Saturation) + Math.Abs(Brightness - other.Brightness);
    }
}

/// <summary>
/// A contiguous region of the same colour on the canvas.
/// The planner decides whether to fill or draw this pixel-by-pixel.
/// </summary>
public class DrawRegion
{
    public DrawColour Colour { get; init; } = default!;

    /// <summary>All pixel coordinates in this region (canvas space, 0-255).</summary>
    public List<(int X, int Y)> Pixels { get; init; } = [];

    /// <summary>
    /// True if this region is large enough to benefit from the fill tool.
    /// Threshold is tunable — fill tool has overhead (tool swap + navigate back).
    /// </summary>
    public bool UseFill => Pixels.Count >= FillThreshold;

    /// <summary>Minimum pixel count to justify using the fill tool.</summary>
    public const int FillThreshold = int.MaxValue; // Fill tool disabled until seed selection is reliable

    /// <summary>Bounding box — used for fill seed point selection.</summary>
    public (int MinX, int MinY, int MaxX, int MaxY) Bounds =>
        (Pixels.Min(p => p.X), Pixels.Min(p => p.Y),
         Pixels.Max(p => p.X), Pixels.Max(p => p.Y));
}

/// <summary>
/// The full ordered plan for drawing an image.
/// Regions are sorted to minimise colour-swap count and cursor travel.
/// </summary>
public class DrawPlan
{
    public List<DrawRegion> Regions { get; init; } = [];
    public int TotalPixels => Regions.Sum(r => r.Pixels.Count);
    public int EstimatedSteps { get; set; }
}
