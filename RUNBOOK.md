# TomodachiDraw Production Runbook

## Quick Start

### 1. Prep
```bash
# SSH to Pi
ssh pi@192.168.1.44

# Check service status
sudo systemctl status tomodachidraw

# If not running:
sudo systemctl start tomodachidraw
```

### 2. Access Web UI
- Open `http://192.168.1.44:5000` in browser
- Should see Switch connection panel + draw panel

### 3. Pair Switch (if needed)
- Go to System Settings → Controllers → Pair Controllers on Switch
- Click "Start Pairing" button in web UI
- Wait for "Connected" status

### 4. Upload Image & Draw
- Click file input, select PNG (256×256 preferred)
- Adjust max colours if needed (8-16 recommended)
- Click "Quantize & Preview" to see result
- Click "▶ Start Drawing" to begin

### 5. Monitor
- Progress bar shows % complete
- Status text shows current pass/row
- Can pause/resume/cancel at any time

### 6. Finish
- Wait for "Done! ✓" message
- Check canvas on Switch for result

## Debug Panel Guide

Located at bottom of web UI (🐛 Debug section).

### Mode Toggle
- **Safe**: Full realignment before each operation (slow, bulletproof)
- **Speed**: Trust tracked state (fast, potential drift risk)

### Test Buttons
- **Tool**: Switch between Pencil, Fill, Eraser
- **Palette**: Activate slots, set test colours (Red, Black)
- **Canvas**: Navigate to key positions (centre, origin, corner)
- **Draw**: Test pixel and fill operations
- **State**: Realign to known position (128,128)

Use these to isolate issues — e.g., if palette isn't changing, test SetPaletteSlot independently.

## Tuning

### Draw Speed Slider
Located in "Calibration" panel:
- **Lower** (10-30ms): Faster, but risk of missed inputs
- **Higher** (80-150ms): Slower, but very reliable
- Default: 80ms (conservative)

Recommended: Start at 80ms, reduce by 10ms increments if draw time is too long.

### Fill Threshold
Controls minimum region size to use fill tool:
- **0**: Disable fill (draw everything pixel-by-pixel)
- **30**: Use fill for regions ≥30px (default, good balance)
- **100**: Only use fill for large regions (more manual work)

## Common Issues

### "Controller not connected"
- Check Switch pairing: System Settings → Controllers → Check if Pro Controller is listed
- Re-pair: disconnect controller in Settings, then use "Reconnect (no grip menu)" button

### Pixels look misaligned or skip
- Increase draw speed slider (more conservative timing)
- Try Safe mode instead of Speed mode in debug panel
- If issue persists, realign from debug panel and retry

### Fill fills entire canvas
- Verify outline was drawn first (fill only works inside bounded regions)
- Check fill seed is inside the intended region
- May need to increase fill threshold or disable fill altogether

### Some colours wrong
- Check colour distance settings (very bright/saturated colours may not match)
- Test palette setup from debug panel
- Try reducing max colours during quantisation

## Emergency Controls

**Pause** (via web UI):
- Stops drawing immediately, holds current position
- Resume to continue from where it paused

**Cancel** (via web UI):
- Stops draw and releases all buttons
- Safe to do at any time
- Service stays running

**Restart Service** (via SSH):
```bash
sudo systemctl restart tomodachidraw
```
- Clears all state, resets connection
- Service will reconnect to Switch

**Restart Pi** (last resort):
```bash
sudo reboot
```
- Takes ~30 seconds
- Service auto-starts on boot

## Performance Targets

| Image Size | Draw Time (Safe) | Draw Time (Speed) | Efficiency |
|------------|-----------------|-------------------|------------|
| 50×50      | ~5s             | ~2s               | 30-40 ops/s |
| 100×100    | ~15s            | ~8s               | 25-35 ops/s |
| 256×256    | ~60s            | ~30s              | 20-30 ops/s |

(Actual times depend on image complexity, region size distribution, colour count)

## Post-Draw Checklist

- [ ] Draw completed without errors
- [ ] Image visible on Switch canvas
- [ ] Colours match expected palette
- [ ] No visible pixel misalignment
- [ ] All pixels present (no skips)

## Logs & Debugging

Service logs:
```bash
sudo journalctl -u tomodachidraw -f  # Follow logs in real-time
```

Build & start fresh:
```bash
cd ~/TomodachiDraw/TomodachiDraw
~/.dotnet/dotnet clean
~/.dotnet/dotnet build
sudo systemctl restart tomodachidraw
```

## References

- **Canvas API**: Abstracts all Switch interaction, see `Services/CanvasAPI.cs`
- **Region Detection**: Flood-fill + outline, see `Services/RegionDetector.cs`
- **Draw Orchestrator**: Main loop, see `Services/DrawOrchestrator.cs`
- **Test Checklist**: See `TESTING.md`
