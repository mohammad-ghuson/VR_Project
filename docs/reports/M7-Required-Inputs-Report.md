# M7 — Required-Input Gaps Closed (PDF §4)

**Date:** 2026-07-07
**Scope:** Add the five simulation inputs that the official PDF names in its evaluation
criteria but the project did not yet expose: **canvas tilt, air resistance, humidity,
swing count, and canvas surface type.** After this batch, **every §4 evaluation-named
input is present in the runtime UI and written into the saved experiment report.**

All logic is hand-written C# — no Unity physics engine, no ready-made packages — consistent
with the project constraint.

---

## 1. Canvas tilt (وضعية اللوحة: مستوية/مائلة)

- **Control:** "Canvas Tilt" slider, 0–60°, in the Environment panel.
- **Implementation:** rotates the canvas about its local X axis
  (`localRotation = Euler(v,0,0)`). The paint hit-test is an **analytic plane** derived from
  the canvas transform (centre + normal + axes), so it follows any orientation *for free* —
  no special-casing. Zero tilt = unchanged behaviour.
- **Files:** `UIControlPanel.cs` (bind), `LiquidSurfaceTools.cs` (slider).

## 2. Air resistance (مقاومة الهواء)

- **Control:** "Air Resistance" slider, 0–1, in the Environment panel. One slider drives two
  coupled effects:
  - **Falling paint:** a linear drag `a -= k·v` on each free-falling (escaped) particle,
    giving a terminal velocity (`k = slider·3`).
  - **Pendulum:** the swing amplitude decays as `exp(-airDamping·t)` so the bucket gradually
    settles (`airDamping = slider·0.4`).
- **0 = no damping = unchanged behaviour.**
- **Files:** `Bucket.cs` (amplitude decay), `SphFluid.cs` (particle drag),
  `UIControlPanel.cs`, `LiquidSurfaceTools.cs`.

## 3. Humidity → splat spread (رطوبة الهواء)

- **Control:** "Humidity" slider, 0–1, in the Environment panel.
- **Model:** a wetter surface keeps paint wet, so each droplet spreads wider. The effective
  stamp radius is `splatRadius · (1 + humidity·1.5)` — humidity 0 → ×1, humidity 1 → ×2.5.
- **Independent from "Splat Width":** Splat Width is a property of the *paint droplet*;
  humidity is an *environmental* amplifier. They multiply. This separation is the interview
  answer to "how is humidity different from splat width?".
- **Files:** `SphFluid.cs` (field + effective radius at the stamp call), `UIControlPanel.cs`,
  `LiquidSurfaceTools.cs`.

## 4. Swing count (عدد مرات التأرجح)

- **Control:** "Swing Count" slider (whole numbers, 0–10) in the main panel's Motion section.
- **Behaviour:** `0 = unlimited` (unchanged). For `N > 0` the bucket performs N full swings
  then **settles at the vertical rest position**. The stop time is chosen at a
  bottom-crossing — `t = (2N + 0.5)·π/ω` — where the natural pendulum angle is already ~0,
  so there is **no visual snap**. Circular mode stops after N revolutions.
- **Restart:** changing the count (or pressing Reset) calls `RestartSwing()` (`time = 0`),
  so the swing replays from the beginning.
- **Files:** `Bucket.cs` (`swingCount`, `RestartSwing`, budget logic), `UIControlPanel.cs`,
  `LiquidSurfaceTools.cs`.

## 5. Canvas surface type (نوع السطح: قماش/خشب/معدن/ورق)

- **Control:** "Surface" cycling button in the Environment panel (shares one row with the
  Motion button). Click cycles Canvas → Wood → Metal → Paper.
- **Model — each surface differs in colour and absorbency:**

  | Surface | Base colour | Edge softness | Spread ×|
  |---|---|---|---|
  | Canvas | warm off-white | 0.50 | 1.00 |
  | Wood   | light brown    | 0.55 | 1.05 |
  | Metal  | cool grey      | 0.15 (hard) | 0.80 (beads) |
  | Paper  | white          | 0.80 (soft) | 1.25 (bleeds) |

- Switching a surface **repaints the background** with the new base colour (a fresh surface),
  because paint already on the canvas cannot be re-tinted underneath.
- **Files:** `SphFluid.cs` (`surfaceSpread` factor), `UIControlPanel.cs` (presets +
  `ApplySurface`), `LiquidSurfaceTools.cs` (button).

---

## Reporting (PDF §5 output #7)

The saved `.txt` experiment report now records the new inputs alongside the existing ones:
**Air resistance, Humidity, Canvas tilt, Surface type, Swing count**. So a saved experiment
fully reproduces its conditions.

## UI layout note

The Environment panel uses compact rows and now packs six sliders plus a two-button row
(Motion | Surface) without growing. The main panel's row layout is driven entirely by the
**C2 — Polish Panels** editor command, which must always be run **last** (it repositions
every row under its section header). Running any `M5.x` builder after C2 reverts its rows to
raw index positions and causes overlap.

## Constraint compliance

Motion, fluid, collision, and painting remain 100% hand-written C#. GPU is used for rendering
only. No Rigidbody/Collider/Joint, no ready-made fluid or asset packages.

## What remains

- **Justify-or-add:** bucket weight, bucket radius, rope elasticity, suspension point,
  swing-plane direction.
- **M8:** realism tuning vs real artworks, performance pass, code cleanup (superseded
  `LiquidController.cs` + disc menus, editor deprecation warnings).
