# TomodachiDraw Changelog

## [1.0.0] - 2026-04-22 — Production Release 🚀

### ✨ New Features
- **Safe/Speed Modes** — Toggle between bulletproof and fast draw
- **Canvas API** — Clean abstraction hiding all input complexity
- **Debug Panel** — Collapsible section on main page with:
  - Colour picker (HSB sliders + preview)
  - Calibration controls (draw speed, colour rect, hue steps)
  - No separate page (stays on main UI)
- **Efficient Drawing** — Outline + fill for large solid areas
- **Colour Quantisation** — Reduce image to 0-256 colours
- **Manual Controls** — Full on-screen d-pad + button grid
- **Gamepad Passthrough** — Browser gamepad → Switch controller

### 🔧 Architecture
- **CanvasAPI** — High-level drawing interface (SetTool, Navigate, Draw, Fill)
- **DrawOrchestrator** — Executes draw commands with cancel support
- **CanvasNavigatorService** — Canvas position tracking + tool switching
- **ImageProcessorService** — PNG → draw commands via region detection
- **RegionDetector** — Flood-fill detection + outline computation

### 📚 Documentation
- **README.md** — Quick start + architecture overview
- **RUNBOOK.md** — Step-by-step production guide
- **TESTING.md** — Pre-production validation checklist
- **GitHub Wiki** — Detailed troubleshooting

### 🐛 Bug Fixes
- Cancel button now respects cancellation tokens
- Navigation auto-initializes cursor in Speed mode
- Debug panel no longer causes Razor compilation errors
- Proper error handling in all async operations

### ⚙️ Technical
- .NET 8 (net8.0)
- Blazor Server (real-time UI updates)
- OpenRouter SSE for AI (disabled in production build)
- joycontrol for Bluetooth controller emulation
- ImageSharp for image processing
- RegionDetector for efficient fill detection

### 📊 Performance
- 256×256 images: 30-60s Speed mode, 60s+ Safe mode
- 80ms dpad settle (conservative, configurable)
- 20-40 commands/second throughput
- Bluetooth latency: 50-100ms inherent

### Known Issues
- Colour matching <0.02f threshold (bright colours may drift)
- Speed mode can accumulate drift if inputs missed
- Fill only works inside drawn boundaries
- Bluetooth inherently slower than USB

### Migration from v0.x
- Requires full rebuild (breaking Canvas API changes)
- Calibration values reset (use debug panel to retune)
- Old debug.html removed (now integrated to main page)

---

## Future Roadmap

### v1.1 (Q2 2026)
- [ ] USB controller support
- [ ] Input pipelining (batch sequences)
- [ ] Performance dashboard

### v2.0 (TBD)
- [ ] Mii maker support
- [ ] Local model fallback (no internet required)
- [ ] Web export (draw to PNG instead of Switch)

---

**See TESTING.md for pre-production checklist.**
**See RUNBOOK.md for production workflow.**
