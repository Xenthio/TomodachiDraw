# TomodachiDraw

PNG → Switch drawing tool for Tomodachi Life 2. Runs a Blazor web UI on the Raspberry Pi and drives a Pro Controller emulator over Bluetooth.

## Setup (on the Pi)

### 1. Install .NET 8
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
echo 'export PATH=$PATH:~/.dotnet' >> ~/.bashrc && source ~/.bashrc
```

### 2. Install joycontrol
```bash
sudo apt update
sudo apt install -y python3-pip python3-dbus libhidapi-hidraw0 bluez
pip3 install joycontrol
# Allow joycontrol to run without password prompt:
echo "pi ALL=(ALL) NOPASSWD: /usr/bin/python3 -m joycontrol*" | sudo tee /etc/sudoers.d/joycontrol
```

### 3. Build and run
```bash
cd ~/TomodachiDraw
dotnet run --project TomodachiDraw
```

Web UI will be at **http://[pi-ip]:5000** — open it on any device on your network.

## Canvas calibration (already encoded in code)

- Hold UP+LEFT until cursor hits screen edge
- Press DOWN 76 times → top of canvas
- Press RIGHT 192 times → left edge of canvas
- That's (0, 0)

## Architecture

```
SwitchControllerService   ← wraps joycontrol subprocess, raw button API
CanvasNavigatorService    ← canvas coordinates, tool/palette switching
ImageProcessorService     ← PNG → DrawPlan (regions, colour, order)
DrawOrchestrator          ← drives the above, progress reporting
Pages/Index.razor         ← Blazor web UI
```

## Notes / TODOs

- Colour picker calibration: slider step sizes (currently estimated at 1/100) need
  measuring against the actual game. Tune `SetSliderAsync` if colours are off.
- Fill tool seed pixel: currently uses median pixel of region — may need to pick
  a pixel more reliably inside the region boundary.
- Eyedropper reuse: could use ZR+hover to sample existing canvas colours instead
  of re-entering HSB values. Useful for multi-session resumption.
- Mii maker: `SwitchControllerService` and `CanvasNavigatorService` are reusable.
  Mii input will need its own navigator layer.
