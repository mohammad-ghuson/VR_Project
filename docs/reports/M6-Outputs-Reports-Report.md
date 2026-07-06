# Milestone Report — M6: Experiment Outputs, Report & Comparison

**Project:** Swinging Paint Bucket (VR) · Unity 6000.4.10f1 · URP 17.4
**Goal (PDF §5, outputs 5–7):** show the values used in the experiment, generate a report
(inputs, motion time, number of trails, colour spread area), and compare experiments.
**Status:** Complete (M6.1–M6.3). With this, **all seven PDF outputs are done.**

---

## 1. Files

| File | Role |
|---|---|
| `Assets/Scripts/Liquid/PaintCanvas.cs` | Trail counter + incremental colour-coverage counter |
| `Assets/Scripts/Liquid/SphFluid.cs` | Experiment clock, paint-used counter, on-screen experiment line (shadowed HUD) |
| `Assets/Scripts/Liquid/UIControlPanel.cs` | `SaveExperiment()` writes the text report next to the PNG; snapshot + live comparison table |
| `Assets/Scripts/Liquid/Editor/LiquidSurfaceTools.cs` | Menu `UI - M6.3 Add Comparison Panel` |

---

## 2. How we built it

### M6.1 — Live experiment statistics (output 5 + report metrics)
- **Motion time**: a clock that starts at the first outflow and stops once every particle is
  consumed — the actual painting duration.
- **Number of trails**: a stamp that does not connect to the previous one starts a NEW trail
  (connected stamps continue the current one), so the count matches what the eye sees.
- **Colour spread area**: an incremental counter — each pixel is counted once, when it first
  receives a visible amount of paint (≥10% blend). No full-texture scans; zero per-frame cost.
- Shown on a second HUD line: `Experiment  time / trails / coverage / paint-used`, drawn with
  a drop-shadow so it stays readable over the white canvas.

### M6.2 — Text report saved with the image (output 7)
`Save PNG` now calls `SaveExperiment()`: saves the PNG, then writes a same-name `.txt` beside
it containing **[Inputs]** (rope length, release angle, speed, motion mode, gravity, wall
bounce, viscosity, hole diameter, splat width, paint colour RGB, paint amount, canvas size)
and **[Results]** (motion time, paint used, number of trails, colour spread %) — exactly the
four report items the PDF names.

### M6.3 — Experiment comparison (output 6)
Saving an experiment also captures a snapshot; a right-side "Experiment Comparison" panel
shows a live `previous -> current` table (9 inputs + 4 results, refreshed 4×/s). Workflow:
run → Save → change values → Clear Canvas → run again → read the two columns side by side.
Each further Save promotes the current experiment to the "previous" column.

---

## 3. How to test
1. `Tools → Liquid → UI - M6.3 Add Comparison Panel`, press Play.
2. Let it paint, press **Save PNG** → `SavedPaintings/painting_<t>.png` + `painting_<t>.txt`.
3. Change colour/angle, **Clear Canvas**, run again → the comparison table shows old vs new
   inputs and results diverging live.

## 4. Definitions (if asked)
- Motion time = first-outflow → all-paint-consumed (resets on respawn/Apply).
- A trail = a maximal chain of connected stamps; Clear Canvas resets trails/coverage.
- Coverage = % of canvas pixels ever painted at ≥10% blend (threshold documented).

## 5. Notes / next
- Remaining roadmap: required-input gaps (canvas tilt, air resistance, humidity, surface
  type, swing count — named in the PDF evaluation criteria), then M8 (tuning, performance,
  cleanup). M7 (smooth fluid surface) stays optional.
