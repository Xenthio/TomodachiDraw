using TomodachiDraw.Models;

namespace TomodachiDraw.Services;

/// <summary>
/// Executes a sequence of <see cref="DrawCommand"/>s defensively.
/// 
/// Philosophy: each command is responsible for ensuring preconditions.
/// No state tracking — just execute what we're told.
/// If something breaks, the next command resets us.
/// </summary>
public class DrawOrchestrator(
    CanvasNavigatorService navigator,
    SwitchControllerService controller)
{
    public float   Progress      { get; private set; }
    public string  StatusMessage { get; private set; } = "Idle";
    public bool    IsRunning     { get; private set; }
    public bool    IsPaused      { get; private set; }

    public event Action? ProgressChanged;

    private CancellationTokenSource? _cts;
    private TaskCompletionSource?    _pauseTcs;

    public void Pause()  { IsPaused = true;  _pauseTcs = new TaskCompletionSource(); NotifyProgress(); }
    public void Resume() { IsPaused = false; _pauseTcs?.TrySetResult(); _pauseTcs = null; NotifyProgress(); }
    public void Cancel() => _cts?.Cancel();

    // =========================================================================
    // Execution
    // =========================================================================

    public async Task ExecuteAsync(List<DrawCommand> commands)
    {
        if (IsRunning) return;
        if (!controller.State.IsConnected)
            throw new InvalidOperationException("Switch not connected.");

        IsRunning = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            int done = 0;
            int total = commands.Count;

            foreach (var cmd in commands)
            {
                ct.ThrowIfCancellationRequested();
                await WaitIfPausedAsync(ct);

                SetStatus($"Cmd {done + 1}/{total}: {cmd.GetType().Name}");

                try
                {
                    await ExecuteCommandAsync(cmd, ct);
                }
                catch (Exception ex)
                {
                    SetStatus($"Command failed: {ex.Message}. Attempting recovery…");
                    await Task.Delay(500);  // Brief pause
                    // Continue anyway — next command might reset state
                }

                done++;
                Progress = (float)done / total;
                NotifyProgress();
            }

            SetStatus("Done! ✓");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Fatal error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            NotifyProgress();
        }
    }

    // =========================================================================
    // Command handlers
    // =========================================================================

    private async Task ExecuteCommandAsync(DrawCommand cmd, CancellationToken ct)
    {
        switch (cmd)
        {
            case EnsureCanvasFocus:
                await navigator.EnsureCanvasFocusAsync();
                break;

            case AnchorToOrigin:
                await navigator.HomeCursorAsync();
                break;

            case SetPaletteSlot setPal:
                await navigator.SetPaletteSlotColourAsync(setPal.Slot, setPal.Colour);
                break;

            case ActivateSlot act:
                await navigator.ActivatePaletteSlotAsync(act.Slot);
                break;

            case NavigateTo nav:
                await navigator.MoveToAsync(nav.X, nav.Y);
                break;

            case DrawPixel:
                await navigator.DrawPixelAsync();
                break;

            case FillRegion:
                await navigator.FillAsync();
                break;

            case SelectTool sel:
                await navigator.SelectToolAsync(sel.Tool);
                break;

            case Wait wait:
                await Task.Delay(wait.Ms);
                break;

            default:
                throw new InvalidOperationException($"Unknown command type: {cmd.GetType().Name}");
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task WaitIfPausedAsync(CancellationToken ct)
    {
        if (_pauseTcs != null)
            await _pauseTcs.Task.WaitAsync(ct);
    }

    private void SetStatus(string msg) { StatusMessage = msg; NotifyProgress(); }
    private void NotifyProgress()      => ProgressChanged?.Invoke();
}
