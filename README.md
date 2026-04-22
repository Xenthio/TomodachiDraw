# TomodachiDraw

**PNG → Tomodachi Life 2 Canvas** via Bluetooth-emulated Pro Controller.

Runs a Blazor web UI on the Raspberry Pi. Detects image regions, draws outlines, and uses the fill tool to render efficiently. Clean Canvas API abstraction with Safe/Speed modes for testing and tuning.

**Status**: Production-ready ✓

## Quick Start

- See **[RUNBOOK.md](RUNBOOK.md)** for how to use
- See **[TESTING.md](TESTING.md)** for pre-run checklist

## Setup (on the Pi)

### 1. Install .NET 8
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
echo 'export PATH=$PATH:~/.dotnet' >> ~/.bashrc && source ~/.bashrc
```

### 2. Install joycontrol
```bash
sudo apt update && sudo apt install -y python3-pip python3-dbus libhidapi-hidraw0 bluez
pip3 install joycontrol
echo "pi ALL=(ALL) NOPASSWD: /usr/bin/python3 -m joycontrol*" | sudo tee /etc/sudoers.d/joycontrol
```

### 3. Build & run
```bash
cd ~/TomodachiDraw
dotnet run --project TomodachiDraw
```

Web UI: **http://[pi-ip]:5000**

## Key Features

- **Region Detection**: Flood-fill finds contiguous regions
- **Outline + Fill**: Efficient rendering of large solid areas  
- **Canvas API**: Clean abstraction hiding input complexity
- **Debug Panel**: Test operations independently  
- **Safe/Speed Modes**: Toggle between bulletproof and fast
- **Responsive Controls**: Pause/resume/cancel anytime

## Architecture

```
CanvasAPI [Safe|Speed modes]
    ↓
DrawOrchestrator
    ↓
CanvasNavigatorService ← SwitchControllerService
    ↓
ImageProcessorService ← RegionDetector
```

## Performance

- Default: 80ms dpad settle (conservative)
- 256×256 image: ~60s Safe mode, ~30s Speed mode
- Tune via calibration slider (experiment with 40-100ms)

## Known Limitations

- Fill only works inside drawn boundaries
- Bluetooth has 50-100ms latency (USB would be faster)
- Colour matching threshold: < 0.02f (bright/saturated colours may drift)
- Speed mode can accumulate drift if inputs are missed (use Safe if needed)

## TODOs / Future

- [ ] USB controller support (eliminate Bluetooth latency)
- [ ] Input pipelining (batch sequences in joycontrol)
- [ ] Performance profiling dashboard
- [ ] Mii maker navigator (reuse Canvas API)
- [ ] Eyedropper sampling (reuse canvas colours)
