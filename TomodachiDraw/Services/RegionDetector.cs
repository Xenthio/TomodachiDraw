using TomodachiDraw.Models;

namespace TomodachiDraw.Services;

/// <summary>
/// Detects contiguous regions in an image and computes their outlines.
/// Strategy: flood-fill to find regions, then trace the outline (perimeter pixels).
/// </summary>
public static class RegionDetector
{
    /// <summary>A contiguous region with a specific colour.</summary>
    public class Region
    {
        public DrawColour Colour { get; init; } = default!;
        public List<(int X, int Y)> Pixels { get; init; } = [];
        public List<(int X, int Y)> OutlinePixels { get; init; } = [];
        public (int X, int Y) SeedPixel { get; init; }

        public int PixelCount => Pixels.Count;
        public int OutlineCount => OutlinePixels.Count;
    }

    /// <summary>
    /// Detect all contiguous regions in an image.
    /// Returns list of regions with their outline pixels computed.
    /// </summary>
    public static List<Region> DetectRegions(byte[] imagePixels, DrawColour[] colourMap, int width, int height)
    {
        var regions = new List<Region>();
        var visited = new bool[width * height];

        for (int idx = 0; idx < width * height; idx++)
        {
            if (visited[idx]) continue;

            int y = idx / width;
            int x = idx % width;
            var colour = colourMap[idx];

            // Flood-fill to find this region
            var regionPixels = FloodFill(colourMap, visited, x, y, colour, width, height);
            if (regionPixels.Count == 0) continue;

            // Find seed (closest to centroid)
            float cx = regionPixels.Average(p => (float)p.X);
            float cy = regionPixels.Average(p => (float)p.Y);
            var seed = regionPixels.MinBy(p =>
                MathF.Sqrt(MathF.Pow(p.X - cx, 2) + MathF.Pow(p.Y - cy, 2)));

            // Compute outline
            var outline = ComputeOutline(regionPixels);

            regions.Add(new Region
            {
                Colour = colour,
                Pixels = regionPixels,
                OutlinePixels = outline,
                SeedPixel = seed
            });
        }

        return regions;
    }

    private static List<(int X, int Y)> FloodFill(
        DrawColour[] colourMap, bool[] visited, int startX, int startY,
        DrawColour targetColour, int width, int height)
    {
        var pixels = new List<(int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || x >= width || y < 0 || y >= height) continue;

            int idx = y * width + x;
            if (visited[idx]) continue;

            var c = colourMap[idx];
            if (c == null || c.DistanceTo(targetColour) > 0.02f) continue;

            visited[idx] = true;
            pixels.Add((x, y));

            queue.Enqueue((x + 1, y));
            queue.Enqueue((x - 1, y));
            queue.Enqueue((x, y + 1));
            queue.Enqueue((x, y - 1));
        }

        return pixels;
    }

    /// <summary>
    /// Extract outline pixels from a region.
    /// Outline = pixels that have at least one non-region neighbour.
    /// </summary>
    private static List<(int X, int Y)> ComputeOutline(List<(int X, int Y)> regionPixels)
    {
        var pixelSet = new HashSet<(int, int)>(regionPixels);
        var outline = new List<(int X, int Y)>();

        foreach (var (x, y) in regionPixels)
        {
            // Check if this pixel is on the boundary
            bool isOutline = false;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!pixelSet.Contains((x + dx, y + dy)))
                    {
                        isOutline = true;
                        break;
                    }
                }

            if (isOutline)
                outline.Add((x, y));
        }

        return outline;
    }
}
