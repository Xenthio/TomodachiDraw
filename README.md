# TomodachiDraw

**PNG → Tomodachi Life 2 Canvas** via Bluetooth-emulated Pro Controller.

Runs a Blazor web UI on the Raspberry Pi. Detects image regions, draws outlines, and uses the fill tool to render efficiently. Safe/Speed modes for testing and production tuning.

**Status**: ✓ Production-ready

## Quick Start

1. **Import PNG** — Load an image, adjust max colours
2. **Connect Switch** — Pair your Bluetooth Pro Controller  
3. **Calibrate (optional)** — Test colours, tweak draw speed
4. **Draw** — Hit the Start button, watch it paint!

All controls are on one page. Debug/calibration is in the collapsible **🔧 Debug / Calibration** section.

## Setup (on the Raspberry Pi)

### Dependencies
```bash
sudo apt update && sudo apt install -y python3-pip python3-dbus libhidapi-hidraw0 bluez
pip3 install joycontrol
echo "pi ALL=(ALL) NOPASSWD: /usr/bin/python3 -m joycontrol*" | sudo tee /etc/sudoers.d/joycontrol
```

### Install & Run
```bash
cd ~/TomodachiDraw
dotnet build
sudo systemctl restart tomodachidraw
```

Web UI: **http://[pi-ip]:5000**

## Architecture

```
CanvasAPI [Safe/Speed modes]     ← high-level interface
    ↓
DrawOrchestrator                  ← orchestrates draw commands
    ↓
CanvasNavigatorService            ← canvas navigation + tools
SwitchControllerService           ← joycontrol bridge
    ↓
ImageProcessorService             ← PNG → draw commands
RegionDetector                    ← flood-fill + outlines
```

## Key Features

- **Two Draw Modes**:
  - **Safe**: Full realignment before each op (slow, bulletproof)
  - **Speed**: Trust tracked state, minimal inputs (fast, drift risk)
  
- **Efficient Rendering**:
  - Flood-fill region detection
  - Outline drawing + fill tool combo
  - Colour quantisation (0-256 colours)

- **Calibration Panel**:
  - HSB colour picker with preview
  - Draw speed tuning (dpad settle)
  - Colour picker rect sizing
  - Hue slider step adjustment

- **Full Manual Control**:
  - On-screen d-pad + buttons
  - Gamepad passthrough (browser → Switch)
  - Pause/resume/cancel anytime

## Performance

- **Default**: 80ms dpad settle (conservative, reliable)
- **256×256 image**: ~30-60s Speed mode, ~60s+ Safe mode
- **Typical flow**: Quantise → Analyse → Realign → Draw

Tune via calibration sliders if needed (experiment with 40-100ms).

## Modes Explained

### Safe Mode
- Before each operation: realign to home (128, 128)
- Prevents state drift
- Slower (extra inputs per operation)
- **Use when**: Testing, troubleshooting, or if Speed mode drifts

### Speed Mode  
- Trust the tracked cursor position
- Navigate relatively (only needed inputs)
- Faster (skip redundant realignments)
- **Use when**: Production runs, optimizing speed

Toggle between modes in the debug panel anytime, even mid-run.

## Known Limitations

- Fill tool only works inside drawn boundaries
- Bluetooth has 50-100ms latency inherent
- Colour matching threshold: <0.02f (bright/saturated may drift)
- Speed mode can accumulate drift if inputs are missed

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Colours don't match | Use colour test panel; tune colour rect width/height |
| Drawing is too slow | Switch to Speed mode, reduce dpad settle |
| Navigation overshoots | Use Safe mode for that op, or realign |
| Fill doesn't work | Check outline was drawn; click canvas first |
| Pairing fails | Make sure Switch is in controller pairing mode |

## Future Improvements

- [ ] USB controller support (eliminate Bluetooth latency)
- [ ] Input pipelining (batch dpad sequences)
- [ ] Per-row re-alignment option
- [ ] Performance profiling dashboard
- [ ] Mii maker navigator (reuse Canvas API)
- [ ] Eyedropper sampling (reuse canvas colours)

## Documentation

- **TESTING.md** — Pre-production checklist
- **RUNBOOK.md** — Step-by-step production guide
- **API** — `/api/debug` for programmatic testing

## Build Status

- Build: ✓ Clean (0 errors)
- Tests: Manual validation in debug panel
- CI: GitHub Actions (basic build)

---

**Ready to draw!** 🎨
