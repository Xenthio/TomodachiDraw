using System.Diagnostics;
using TomodachiDraw.Models;

namespace TomodachiDraw.Services;

/// <summary>
/// Manages the joycontrol bridge process and translates high-level button commands
/// into bridge stdin commands. The bridge script handles all async joycontrol internals
/// and reports status via stdout lines prefixed with "STATUS:".
///
/// Bridge script: ~/TomodachiDraw/joycontrol_bridge.py
/// Protocol:
///   stdin  → "btn <button>"  press/hold button
///            "btn -"         release all
///            "quit"          clean exit
///   stdout ← "STATUS:ADVERTISING"
///             "STATUS:CONNECTED"
///             "STATUS:ERROR:<msg>"
/// </summary>
public class SwitchControllerService : IAsyncDisposable
{
    private Process? _bridgeProcess;
    private StreamWriter? _stdin;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ControllerState State { get; } = new();

    /// <summary>Fired when pairing state changes — Blazor UI subscribes to this.</summary>
    public event Action? StateChanged;

    // ---------------------------------------------------------------------------
    // Pairing
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Start the joycontrol bridge and begin advertising as a Pro Controller.
    /// The Switch needs to be in "Change Grip/Order" or controller pairing mode.
    /// </summary>
    public async Task StartPairingAsync(bool reconnect = false)
    {
        if (_bridgeProcess != null)
            await StopAsync();

        State.Pairing = PairingState.Searching;
        State.ErrorMessage = null;
        NotifyStateChanged();

        // Kill any leftover bridge processes holding the BT HID socket
        await RunShellAsync("killall -9 python3");
        await Task.Delay(500);

        // Unblock BT adapter and make sure it's up before starting
        await RunShellAsync("rfkill unblock bluetooth", useSudo: true);
        await RunShellAsync("hciconfig hci0 up", useSudo: true);
        await Task.Delay(300);

        var psi = new ProcessStartInfo
        {
            FileName = "sudo",
            Arguments = $"python3 /home/pi/TomodachiDraw/joycontrol_bridge.py{(reconnect ? " --reconnect" : "")}",
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute = false,
        };

        _bridgeProcess = Process.Start(psi)!;
        _stdin = _bridgeProcess.StandardInput;

        // Watch stdout for STATUS: lines
        _ = Task.Run(async () =>
        {
            while (!_bridgeProcess.StandardOutput.EndOfStream)
            {
                var line = await _bridgeProcess.StandardOutput.ReadLineAsync();
                if (line == null) break;

                if (line.StartsWith("STATUS:"))
                {
                    var status = line["STATUS:".Length..];
                    if (status == "ADVERTISING")
                    {
                        State.Pairing = PairingState.Searching;
                    }
                    else if (status == "CONNECTED")
                    {
                        State.Pairing = PairingState.Connected;
                    }
                    else if (status.StartsWith("ERROR:"))
                    {
                        State.ErrorMessage = status["ERROR:".Length..];
                        State.Pairing = PairingState.Error;
                    }
                    NotifyStateChanged();
                }
            }

            // Process exited
            if (State.Pairing != PairingState.Disconnected)
            {
                State.Pairing = PairingState.Disconnected;
                NotifyStateChanged();
            }
        });

        // Log stderr for debugging
        _ = Task.Run(async () =>
        {
            while (!_bridgeProcess.StandardError.EndOfStream)
            {
                await _bridgeProcess.StandardError.ReadLineAsync();
                // Could surface critical errors here if needed
            }
        });
    }

    public async Task StopAsync()
    {
        if (_bridgeProcess is { HasExited: false })
        {
            await SendCommandAsync("quit");
            await Task.Delay(500);
            if (!_bridgeProcess.HasExited)
                _bridgeProcess.Kill();
            await _bridgeProcess.WaitForExitAsync();
        }
        _bridgeProcess = null;
        _stdin = null;
        State.Pairing = PairingState.Disconnected;
        NotifyStateChanged();
    }

    // ---------------------------------------------------------------------------
    // Raw input primitives
    // ---------------------------------------------------------------------------

    /// <summary>Press and release a button (default 80ms hold).</summary>
    public async Task PressAsync(string button, int holdMs = 80)
    {
        await SendCommandAsync($"btn {button}");
        await Task.Delay(holdMs);
        await SendCommandAsync("btn -");
        await Task.Delay(40);
    }

    /// <summary>Set left stick to a direction. Direction: up/down/left/right/upleft/center</summary>
    public async Task StickAsync(string direction)
    {
        await SendCommandAsync($"stick l {direction}");
        await Task.Delay(30);
    }

    /// <summary>Hold a button down without releasing.</summary>
    public async Task HoldAsync(string button)
        => await SendCommandAsync($"btn {button}");

    /// <summary>Release all held buttons.</summary>
    public async Task ReleaseAsync()
        => await SendCommandAsync("btn -");

    /// <summary>Press dpad in a direction N times.</summary>
    public async Task DpadAsync(string direction, int count = 1, int delayMs = 60)
    {
        // joycontrol Pro Controller uses plain direction names: up/down/left/right
        for (int i = 0; i < count; i++)
        {
            await SendCommandAsync($"btn {direction}");
            await Task.Delay(60);   // hold
            await SendCommandAsync("btn -");
            await Task.Delay(delayMs); // settle between presses
        }
    }

    /// <summary>Hold a dpad direction for a duration then release.</summary>
    public async Task DpadHoldAsync(string direction, int holdMs)
    {
        await SendCommandAsync($"btn {direction}");
        try { await Task.Delay(holdMs); }
        finally
        {
            await SendCommandAsync("btn -");
            await Task.Delay(50);
        }
    }

    // ---------------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------------

    private async Task SendCommandAsync(string command)
    {
        if (_stdin == null) return;
        await _writeLock.WaitAsync();
        try
        {
            await _stdin.WriteLineAsync(command);
            await _stdin.FlushAsync();
        }
        catch
        {
            // Bridge died — ignore, state will update via stdout monitor
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Run a shell command, optionally as sudo.</summary>
    private static async Task RunShellAsync(string command, bool useSudo = false)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (useSudo) { psi.FileName = "sudo"; psi.Arguments = command; }
            else { var parts = command.Split(' ', 2); psi.FileName = parts[0]; psi.Arguments = parts.Length > 1 ? parts[1] : ""; }
            var p = Process.Start(psi)!;
            await p.WaitForExitAsync();
        }
        catch { }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public async ValueTask DisposeAsync() => await StopAsync();
}
