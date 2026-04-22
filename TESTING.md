# TomodachiDraw Testing & Deployment Checklist

## Pre-Production Readiness

### 1. Canvas API Verification
- [ ] **Safe Mode Tests** (full realignment, bulletproof)
  - [ ] SetTool(Pencil) → success
  - [ ] SetTool(Fill) → success
  - [ ] ActivatePaletteSlot(0..8) → success
  - [ ] SetPaletteColour(slot, colour) → success
  - [ ] NavigateTo(x, y) → cursor moves correctly
  - [ ] DrawPixel() → single pixel at correct position
  - [ ] FillRegion() → fills bounded region correctly

- [ ] **Speed Mode Tests** (trust tracked state, delta navigation)
  - [ ] Toggle to Speed mode
  - [ ] SetTool without full realignment
  - [ ] ActivatePaletteSlot with local navigation
  - [ ] NavigateTo with relative movement
  - [ ] Verify no accumulated drift after 10 operations

- [ ] **Mode Switching Tests**
  - [ ] Safe → Speed → Safe transitions
  - [ ] State consistency maintained

### 2. Region Detection & Fill Pipeline
- [ ] Load test image (PNG, 256×256)
- [ ] Verify region detection outputs correct regions
- [ ] Verify outline computation (perimeter pixels correct)
- [ ] Verify fill seed placement (inside region, near centroid)
- [ ] Test both large regions (use fill) and small regions (pixel-by-pixel)

### 3. Timing Calibration (Optional, for Speed Mode Tuning)
- [ ] Test current defaults (80ms dpad settle)
- [ ] Measure input drop rate at 80ms, 60ms, 40ms, 20ms
- [ ] Note minimum settle time before inputs miss
- [ ] Document findings (e.g., "60ms is sweet spot, 40ms drops 5% of inputs")

### 4. Integration Test (Full Draw)
- [ ] Load test image (any PNG)
- [ ] Generate draw commands
- [ ] Execute in Safe mode first (slow but reliable)
- [ ] Verify image draws correctly
- [ ] Check for misalignments, dropped pixels, colour mismatches
- [ ] Repeat in Speed mode if safe mode successful

### 5. Error Recovery
- [ ] Pause during draw → Resume
- [ ] Cancel during draw → state clean
- [ ] Network disconnect → service restart
- [ ] Unplug controller → graceful shutdown, reconnect works

### 6. Performance Metrics
- [ ] Measure draw time for small image (~10s expected)
- [ ] Measure draw time for medium image (~30-60s expected)
- [ ] Log command count vs region count (sanity check)
- [ ] Calculate input efficiency (commands sent / pixels drawn)

## Deployment Steps

1. **Verify Service Status**
   ```bash
   ssh pi@192.168.1.44 'sudo systemctl status tomodachidraw'
   ```

2. **Access Web UI**
   - Open `http://192.168.1.44:5000` in browser
   - Pair/reconnect Switch controller
   - Check Gamepad status indicator

3. **Run Test Suite**
   - Use Debug panel to test individual operations
   - Toggle Safe/Speed modes
   - Test palette, tool, navigation, drawing

4. **Load & Draw**
   - Upload PNG image
   - Set max colours (keep ≤16 for faster processing)
   - Start draw in Safe mode initially
   - Monitor progress, check for issues
   - If successful, try Speed mode for faster subsequent runs

5. **Monitor**
   - Watch progress bar
   - Note any stalls or jumps
   - Log any errors

## Known Limitations

- Fill tool only works inside bounded regions (outline must be drawn first)
- Bluetooth timing is ~50-100ms conservative (fast Ethernet would be better)
- State tracking in Speed mode can drift if inputs are missed (use Safe mode if drift detected)
- Colour matching uses distance < 0.02f (watch for rounding errors on very bright/saturated colours)

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Debug panel not showing | Hard refresh (Ctrl+F5), clear browser cache |
| Palette not changing | Verify colour distance < 0.02f, check colour rect calibration |
| Pixels not drawing | Verify tool is Pencil, check cursor position |
| Fill filling whole canvas | Verify outline was drawn first, check seed pixel placement |
| Dpad inputs missing | Increase settle time in calibration slider |
| Controller disconnects | Restart Pi service, re-pair Switch |

## Success Criteria

- ✅ All Canvas API operations work in both modes
- ✅ Image draws without visible misalignment
- ✅ Draw completes without errors
- ✅ Colours match expected palette
- ✅ No dropped pixels or input timeouts
