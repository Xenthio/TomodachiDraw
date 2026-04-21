using TomodachiDraw.Models;
using static TomodachiDraw.Services.CanvasNavigatorService;

namespace TomodachiDraw.Services;

/// <summary>
/// Orchestrates the full draw session: takes a <see cref="DrawPlan"/> and
/// drives the <see cref="CanvasNavigatorService"/> to replay it on the Switch.
///
/// Reports progress via <see cref="Progress"/> (0-1) and <see cref="StatusMessage"/>
/// so the Blazor UI can show a live progress bar.
/// </summary>
public class DrawOrchestrator(
    CanvasNavigatorService navigator,
    SwitchControllerService controller)
{
    // -------------------------------------------------------------------------
    // Public state (UI binds to these)
    // -------------------------------------------------------------------------
    public float Progress { get; private set; }
    public string StatusMessage { get; private set; } = "Idle";
    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }

    public event Action? ProgressChanged;

    // -------------------------------------------------------------------------
    // Cancellation / pause
    // -------------------------------------------------------------------------
    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _pauseTcs;

    public void Pause()
    {
        IsPaused = true;
        _pauseTcs = new TaskCompletionSource();
        NotifyProgress();
    }

    public void Resume()
    {
        IsPaused = false;
        _pauseTcs?.TrySetResult();
        _pauseTcs = null;
        NotifyProgress();
    }

    public void Cancel() => _cts?.Cancel();

    // -------------------------------------------------------------------------
    // Main draw loop
    // -------------------------------------------------------------------------

    /// <summary>
    /// Execute the draw plan. This runs asynchronously and updates progress.
    /// Homes the cursor first, then draws region by region.
    /// </summary>
    public async Task DrawAsync(DrawPlan plan)
    {
        if (IsRunning) return;
        if (!controller.State.IsConnected)
            throw new InvalidOperationException("Switch not connected.");

        IsRunning = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            // --- Setup ---
            SetStatus("Setting up: brush, tool, homing cursor…");
            await navigator.SetupAsync();

            // Palette slot assignment: 9-slot cache, keyed by colour similarity
            var paletteSlots = new List<(DrawColour Colour, int Slot)>();
            int nextSlot = 0;

            int done = 0;
            int total = plan.Regions.Count;

            foreach (var region in plan.Regions)
            {
                ct.ThrowIfCancellationRequested();
                await WaitIfPausedAsync(ct);

                // --- Ensure this colour is in the palette ---
                // Use distance comparison instead of exact equality (float rounding after quantise)
                var existing = paletteSlots.FirstOrDefault(s => s.Colour.DistanceTo(region.Colour) < 0.02f);
                int slot;
                if (existing == default)
                {
                    slot = nextSlot % 9;
                    nextSlot++;

                    SetStatus($"Setting palette slot {slot} to H:{region.Colour.Hue:F0} S:{region.Colour.Saturation:F2} B:{region.Colour.Brightness:F2}…");
                    await navigator.SetPaletteSlotColourAsync(slot, region.Colour);
                    paletteSlots.Add((region.Colour, slot));
                }
                else
                {
                    slot = existing.Slot;
                    if (navigator.ActivePaletteSlot != slot)
                    {
                        SetStatus($"Switching to palette slot {slot}…");
                        await navigator.ActivatePaletteSlotAsync(slot);
                    }
                }

                // --- Draw the region ---
                if (region.UseFill)
                {
                    await DrawRegionWithFillAsync(region, ct);
                }
                else
                {
                    await DrawRegionPixelByPixelAsync(region, ct);
                }

                done++;
                Progress = (float)done / total;
                SetStatus($"Drawing… {done}/{total} regions ({Progress:P0})");
            }

            SetStatus("Done! ✓");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            NotifyProgress();
        }
    }

    // -------------------------------------------------------------------------
    // Region drawing strategies
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fill strategy: move to a seed pixel, switch to fill tool, press A,
    /// then switch back to pencil and draw any remaining non-contiguous pixels.
    /// </summary>
    private async Task DrawRegionWithFillAsync(DrawRegion region, CancellationToken ct)
    {
        // Pick the centroid as seed (it's probably inside the region)
        var seed = region.Pixels[region.Pixels.Count / 2];

        await navigator.SelectToolAsync(Tool.Fill);
        await navigator.MoveToAsync(seed.X, seed.Y);
        await navigator.FillAsync();
        await navigator.SelectToolAsync(Tool.Pencil);
    }

    /// <summary>Draw every pixel individually. Optimised traversal order (row-major).</summary>
    private async Task DrawRegionPixelByPixelAsync(DrawRegion region, CancellationToken ct)
    {
        // Sort pixels in a snake pattern (left-right on even rows, right-left on odd)
        // to minimise total cursor travel
        var sorted = region.Pixels
            .OrderBy(p => p.Y)
            .ThenBy(p => p.Y % 2 == 0 ? p.X : -p.X)
            .ToList();

        foreach (var (x, y) in sorted)
        {
            ct.ThrowIfCancellationRequested();
            await WaitIfPausedAsync(ct);

            await navigator.MoveToAsync(x, y);
            await navigator.DrawPixelAsync();
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task WaitIfPausedAsync(CancellationToken ct)
    {
        if (_pauseTcs != null)
            await _pauseTcs.Task.WaitAsync(ct);
    }

    private void SetStatus(string msg)
    {
        StatusMessage = msg;
        NotifyProgress();
    }

    private void NotifyProgress() => ProgressChanged?.Invoke();
}
