using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TomodachiDraw.Models;
using static TomodachiDraw.Services.RegionDetector;

namespace TomodachiDraw.Services;

/// <summary>
/// Builds a **deterministic command sequence** from an image using region outlines + fill.
/// 
/// Pipeline:
///   1. Load + resize + quantize image
///   2. Detect contiguous regions via flood-fill
///   3. For each region: draw outline pixels → then fill interior
///   4. Generate command sequence (outlines first, fill second per region)
/// </summary>
public class ImageProcessorService
{
    /// <summary>Minimum region size to use outline+fill (pixels). Smaller regions drawn pixel-by-pixel.</summary>
    public static int FillThreshold = 30;

    public List<DrawCommand> ProcessImage(byte[] pngBytes, int maxColours, out byte[] previewPng)
    {
        using var image = Image.Load<Rgba32>(pngBytes);
        image.Mutate(ctx => ctx.Resize(256, 256));

        if (maxColours > 0)
            image.Mutate(ctx => ctx.Quantize(
                new SixLabors.ImageSharp.Processing.Processors.Quantization.OctreeQuantizer(
                    new SixLabors.ImageSharp.Processing.Processors.Quantization.QuantizerOptions
                    { MaxColors = maxColours })));

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        previewPng = ms.ToArray();

        return BuildCommandSequence(image);
    }

    public List<DrawCommand> ProcessImage(byte[] pngBytes, int maxColours = 0)
        => ProcessImage(pngBytes, maxColours, out _);

    // =========================================================================

    private static List<DrawCommand> BuildCommandSequence(Image<Rgba32> image)
    {
        var commands = new List<DrawCommand>();

        // --- Convert image to colour map ---
        var colourMap = new DrawColour[256 * 256];
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                var px = image[x, y];
                if (px.A >= 128)
                    colourMap[y * 256 + x] = DrawColour.FromRgb(px.R, px.G, px.B);
            }

        // --- Detect regions ---
        var regions = RegionDetector.DetectRegions([], colourMap, 256, 256);

        // Sort regions by colour frequency (descending) to minimize palette changes
        regions = regions.OrderByDescending(r => r.PixelCount).ToList();

        // --- Assign colours to palette slots ---
        var colourToSlot = new Dictionary<DrawColour, int>(DrawColourComparer.Instance);
        for (int i = 0; i < regions.Count && i < 9; i++)
        {
            colourToSlot[regions[i].Colour] = i % 9;
        }

        // --- Build command sequence ---
        commands.Add(new EnsureCanvasFocus());
        commands.Add(new AnchorToOrigin());

        // Set palette
        for (int i = 0; i < regions.Count; i++)
        {
            int slot = i % 9;
            commands.Add(new SetPaletteSlot(slot, regions[i].Colour));
        }

        // Draw regions: outline + fill
        foreach (var region in regions)
        {
            int slot = colourToSlot[region.Colour];

            commands.Add(new EnsureCanvasFocus());

            // Decide: outline+fill or pixel-by-pixel?
            if (region.PixelCount >= FillThreshold)
            {
                // Draw outline
                var outline = region.OutlinePixels
                    .OrderBy(p => p.Y)
                    .ThenBy(p => p.Y % 2 == 0 ? p.X : -p.X)
                    .ToList();

                foreach (var (x, y) in outline)
                {
                    commands.Add(new NavigateTo(x, y));
                    commands.Add(new DrawPixel());
                }

                // Fill interior
                commands.Add(new NavigateTo(region.SeedPixel.X, region.SeedPixel.Y));
                commands.Add(new SelectTool(CanvasNavigatorService.Tool.Fill));
                commands.Add(new FillRegion());
                commands.Add(new SelectTool(CanvasNavigatorService.Tool.Pencil));
            }
            else
            {
                // Draw all pixels (small region)
                var pixels = region.Pixels
                    .OrderBy(p => p.Y)
                    .ThenBy(p => p.Y % 2 == 0 ? p.X : -p.X)
                    .ToList();

                foreach (var (x, y) in pixels)
                {
                    commands.Add(new NavigateTo(x, y));
                    commands.Add(new DrawPixel());
                }
            }
        }

        commands.Add(new EnsureCanvasFocus());
        return commands;
    }
}

file class DrawColourComparer : IEqualityComparer<DrawColour>
{
    public static readonly DrawColourComparer Instance = new();
    public bool Equals(DrawColour? a, DrawColour? b) =>
        a != null && b != null && a.DistanceTo(b) < 0.02f;
    public int GetHashCode(DrawColour c) =>
        HashCode.Combine(
            MathF.Round(c.Hue / 10) * 10,
            MathF.Round(c.Saturation * 10) / 10,
            MathF.Round(c.Brightness * 10) / 10);
}
