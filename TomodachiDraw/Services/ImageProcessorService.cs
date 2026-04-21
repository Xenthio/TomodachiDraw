using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TomodachiDraw.Models;

namespace TomodachiDraw.Services;

/// <summary>
/// Converts a PNG image into a <see cref="DrawPlan"/> — an optimised sequence
/// of coloured regions ready for the draw orchestrator to replay on the Switch.
///
/// Pipeline:
///   1. Load + resize PNG to 256×256
///   2. Flood-fill to find contiguous same-colour regions
///   3. Sort regions to minimise colour swaps (greedy nearest-colour ordering)
///   4. Within each colour group, order regions to minimise cursor travel
///   5. Mark regions above FillThreshold to use the fill tool
/// </summary>
public class ImageProcessorService
{
    /// <summary>
    /// Process a PNG from a byte array. Returns a ready-to-execute DrawPlan.
    /// </summary>
    /// <param name="pngBytes">Raw PNG/JPEG bytes.</param>
    /// <param name="maxColours">If > 0, quantise image to this many colours first.</param>
    /// <param name="previewPng">Output: the processed 256x256 (quantised) image as PNG bytes for preview.</param>
    public DrawPlan ProcessImage(byte[] pngBytes, int maxColours, out byte[] previewPng)
    {
        using var image = Image.Load<Rgba32>(pngBytes);
        image.Mutate(ctx => ctx.Resize(256, 256));

        if (maxColours > 0)
            image.Mutate(ctx => ctx.Quantize(new SixLabors.ImageSharp.Processing.Processors.Quantization.OctreeQuantizer(
                new SixLabors.ImageSharp.Processing.Processors.Quantization.QuantizerOptions { MaxColors = maxColours })));

        // Encode preview
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        previewPng = ms.ToArray();

        var regions = FindRegions(image);
        var ordered = OptimiseOrder(regions);
        return new DrawPlan { Regions = ordered, EstimatedSteps = EstimateSteps(ordered) };
    }

    // Overload for callers that don't need the preview
    public DrawPlan ProcessImage(byte[] pngBytes, int maxColours = 0)
    {
        return ProcessImage(pngBytes, maxColours, out _);
    }

    // -------------------------------------------------------------------------
    // Region detection via flood fill
    // -------------------------------------------------------------------------

    private static List<DrawRegion> FindRegions(Image<Rgba32> image)
    {
        var visited = new bool[256, 256];
        var regions = new List<DrawRegion>();

        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                if (visited[x, y]) continue;

                var pixel = image[x, y];

                // Skip fully transparent pixels
                if (pixel.A < 128)
                {
                    visited[x, y] = true;
                    continue;
                }

                var colour = DrawColour.FromRgb(pixel.R, pixel.G, pixel.B);
                var region = FloodFill(image, visited, x, y, pixel, colour);
                regions.Add(region);
            }
        }

        return regions;
    }

    /// <summary>4-connected flood fill starting at (startX, startY).</summary>
    private static DrawRegion FloodFill(
        Image<Rgba32> image,
        bool[,] visited,
        int startX, int startY,
        Rgba32 targetPixel,
        DrawColour colour)
    {
        var pixels = new List<(int X, int Y)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((startX, startY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || x >= 256 || y < 0 || y >= 256) continue;
            if (visited[x, y]) continue;

            var px = image[x, y];
            if (!ColourClose(px, targetPixel)) continue;

            visited[x, y] = true;
            pixels.Add((x, y));

            queue.Enqueue((x + 1, y));
            queue.Enqueue((x - 1, y));
            queue.Enqueue((x, y + 1));
            queue.Enqueue((x, y - 1));
        }

        return new DrawRegion { Colour = colour, Pixels = pixels };
    }

    /// <summary>
    /// Two pixels are "the same colour" if each channel is within tolerance.
    /// Tolerance of 4/255 handles JPEG-style compression artefacts.
    /// </summary>
    private static bool ColourClose(Rgba32 a, Rgba32 b, int tolerance = 4) =>
        Math.Abs(a.R - b.R) <= tolerance &&
        Math.Abs(a.G - b.G) <= tolerance &&
        Math.Abs(a.B - b.B) <= tolerance &&
        a.A >= 128 && b.A >= 128;

    // -------------------------------------------------------------------------
    // Ordering optimisation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Greedy nearest-colour ordering to minimise palette swaps.
    /// Within each colour group, regions are ordered by proximity (nearest centroid).
    /// </summary>
    private static List<DrawRegion> OptimiseOrder(List<DrawRegion> regions)
    {
        if (regions.Count == 0) return regions;

        var result   = new List<DrawRegion>(regions.Count);
        var remaining = new List<DrawRegion>(regions);

        // Start with the region closest to canvas origin
        var current = remaining.MinBy(r => Distance(r.Centroid(), (0, 0)))!;
        remaining.Remove(current);
        result.Add(current);

        while (remaining.Count > 0)
        {
            // Find next region: prefer same colour, then nearest centroid
            var sameColour = remaining
                .Where(r => r.Colour.DistanceTo(current.Colour) < 0.05f)
                .ToList();

            var candidates = sameColour.Count > 0 ? sameColour : remaining;
            var next = candidates.MinBy(r => Distance(r.Centroid(), current.Centroid()))!;

            remaining.Remove(next);
            result.Add(next);
            current = next;
        }

        return result;
    }

    private static float Distance((float X, float Y) a, (float X, float Y) b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static int EstimateSteps(List<DrawRegion> regions)
    {
        // Very rough: each pixel = 2 steps (move + draw), fill regions = ~10 overhead
        return regions.Sum(r => r.UseFill ? r.Pixels.Count + 10 : r.Pixels.Count * 2);
    }
}

// Extension helpers
file static class DrawRegionExtensions
{
    public static (float X, float Y) Centroid(this DrawRegion r)
    {
        float x = r.Pixels.Average(p => (float)p.X);
        float y = r.Pixels.Average(p => (float)p.Y);
        return (x, y);
    }
}
