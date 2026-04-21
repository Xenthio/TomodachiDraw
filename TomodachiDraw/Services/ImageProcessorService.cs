using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TomodachiDraw.Models;

namespace TomodachiDraw.Services;

/// <summary>
/// Builds a **deterministic command sequence** from an image.
/// 
/// Strategy: instead of trying to track position/state perfectly, we generate
/// a sequence of **defensive, self-correcting commands**:
/// - Every state change is preceded by an anchor or verify step
/// - Commands assume worst-case (we might be anywhere, in any menu)
/// - Minimize assumptions, maximize explicit resets
/// </summary>
public class ImageProcessorService
{
    public static int FillThreshold = 50;

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

        // --- Parse image into pixels and regions ---
        var pixelsByColour = new Dictionary<DrawColour, List<(int X, int Y)>>(DrawColourComparer.Instance);

        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                var px = image[x, y];
                if (px.A < 128) continue;

                var colour = DrawColour.FromRgb(px.R, px.G, px.B);
                if (!pixelsByColour.ContainsKey(colour))
                    pixelsByColour[colour] = [];
                pixelsByColour[colour].Add((x, y));
            }

        // --- Sort colours by frequency (most common first = least palette changes) ---
        var sortedColours = pixelsByColour
            .OrderByDescending(kv => kv.Value.Count)
            .Select(kv => kv.Key)
            .ToList();

        // --- Generate defensive command sequence ---
        // Start fresh
        commands.Add(new EnsureCanvasFocus());
        commands.Add(new AnchorToOrigin());

        // Assign colours to palette slots (0-8, cycling if needed)
        var colourToSlot = new Dictionary<DrawColour, int>(DrawColourComparer.Instance);
        for (int i = 0; i < sortedColours.Count; i++)
        {
            int slot = i % 9;
            colourToSlot[sortedColours[i]] = slot;
            commands.Add(new SetPaletteSlot(slot, sortedColours[i]));
        }

        // Draw pixels grouped by colour (minimize palette changes)
        int currentSlot = -1;
        foreach (var colour in sortedColours)
        {
            int targetSlot = colourToSlot[colour];

            // Switch slot defensively: always return to canvas first, then switch
            if (targetSlot != currentSlot)
            {
                commands.Add(new EnsureCanvasFocus());
                commands.Add(new ActivateSlot(targetSlot));
                currentSlot = targetSlot;
            }

            // Draw all pixels of this colour
            var pixels = pixelsByColour[colour]
                .OrderBy(p => p.Y)
                .ThenBy(p => p.Y % 2 == 0 ? p.X : -p.X)  // Boustrophedon
                .ToList();

            int lastX = 0, lastY = 0;
            foreach (var (x, y) in pixels)
            {
                commands.Add(new NavigateTo(x, y));
                commands.Add(new DrawPixel());
            }
        }

        // Finish
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
