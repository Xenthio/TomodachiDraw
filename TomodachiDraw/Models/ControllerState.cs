namespace TomodachiDraw.Models;

/// <summary>
/// Current state of the Bluetooth pairing / joycontrol process.
/// </summary>
public enum PairingState
{
    Disconnected,
    Searching,      // joycontrol started, waiting for Switch to see us
    Pairing,        // Switch found us, handshake in progress
    Connected,
    Error
}

/// <summary>
/// Snapshot of the controller's logical state — mostly for UI feedback.
/// </summary>
public class ControllerState
{
    public PairingState Pairing { get; set; } = PairingState.Disconnected;
    public string? ErrorMessage { get; set; }

    // Canvas cursor position (tracked in software — Switch doesn't report it back)
    public int CursorX { get; set; }
    public int CursorY { get; set; }

    public bool IsConnected => Pairing == PairingState.Connected;
}
