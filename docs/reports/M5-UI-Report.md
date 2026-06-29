# Milestone Report — M5: Runtime Control Panel (UI)

**Project:** Swinging Paint Bucket (VR) · Unity 6000.4.10f1 · URP 17.4
**Goal (PDF + supervisor: "drawing first, then interfaces"):** an on-screen control panel
that lets the user drive every physical input live and manage the artwork.
**Status:** Complete (M5.1–M5.5). uGUI only (a UI system, not a physics helper);
all simulation stays our own math in `Bucket` / `SphFluid`.

---

## 1. Files

| File | Role |
|---|---|
| `Assets/Scripts/Liquid/UIControlPanel.cs` | The bridge: holds references to `Bucket` / `SphFluid` / `PaintCanvas`, binds every slider/button to them, and stores defaults for Reset |
| `Assets/Scripts/Liquid/PaintCanvas.cs` | Added `SavePng()` — encodes the canvas texture and writes it to `<Project>/SavedPaintings` |
| `Assets/Scripts/Liquid/Editor/LiquidSurfaceTools.cs` | Menus `UI - M5.1 … M5.5` that build the whole panel from code, plus reusable builders (`BuildControlRow`, `NewSlider`, `NewLabel`, `NewButton`, `BuildColorSwatches`) |

---

## 2. How we built it

### M5.1 — Panel scaffold
A screen-space `Canvas` + `CanvasScaler` + a dark translucent `Panel`, plus an `EventSystem`.
The project uses the new **Input System** package, so the EventSystem is given an
`InputSystemUIInputModule` (resolved by type name so the editor script still compiles if the
package is absent) instead of the legacy `StandaloneInputModule`.

### M5.2 — Physics sliders
Rope length (`bucket.l`), release angle (`bucket.thetaMax`), speed (`bucket.omega`).
Each slider initialises from the current value and pushes changes back live via a listener,
with a numeric read-out beside it.

### M5.3 — Liquid sliders
Viscosity (`viscosity`), hole diameter (`holeDiameter`), splat width (`splatRadius`) — same
binding pattern, two-decimal read-out for the small values.

### M5.4 — Colour + actions
- **M5.4a:** six preset colour swatches **and** R/G/B sliders. Clicking a swatch sets the whole
  `paintColor` and syncs the RGB sliders; the sliders edit individual channels.
- **M5.4b:** a **Hole Open/Close** toggle (label reflects state) and a **Clear Canvas** button.

### M5.5 — Save / Reset
- **Save PNG:** writes the canvas to `<Project>/SavedPaintings/painting_<timestamp>.png`.
- **Reset:** restores the scene's authored defaults (captured at Start) by driving the sliders,
  which re-apply the values to the simulation.

---

## 3. How to test
1. Run the menus in order: `Tools → Liquid → UI - M5.1 … M5.5`.
2. Press Play. Drag the sliders → bucket motion, fluid behaviour, and stroke width respond live.
3. Click a colour swatch / move RGB → paint colour changes. Toggle the hole, clear, and save.
4. The saved PNG appears in `<Project>/SavedPaintings` (full path is logged to the Console).

---

## 4. Notes / next
- The panel is built entirely from code via reusable helpers, so new controls are one call each.
- `SavePng()` uses `Application.dataPath/..` (ideal in the Editor); a standalone build would need
  a build-friendly path — deferred to polish (M8).
- Still pending from earlier feedback: the SPH "compute neighbours once per step" optimisation,
  and the over-the-top spill behaviour (left unchanged by choice) — both for M8.
- Next milestone per the roadmap: **M6** (modelling / visual polish toward the final scene).
