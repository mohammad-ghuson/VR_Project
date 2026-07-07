# M8 — Procedural Bucket, Hanging Rope, Settled Fluid & Cleanup

**Milestone:** M8 (polish pass)
**Scope:** Replace the irregular imported bucket with a hand-built procedural one, fit
the fluid to it, hang it from a proper suspension point with a visible rope, make the
fluid look realistically settled, and clean up superseded code.

All work remains 100% hand-written (no Unity physics engine, no ready-made asset
packages). The GPU is used for rendering only.

---

## 1. Procedural bucket (replaces the imported model)

The downloaded bucket model was irregular: its bounding box was inflated by the handle,
so the analytic SPH container did not match the visible walls (fluid appeared outside),
its size could not be controlled, and it had no clean point to hang a rope from.

`ProceduralBucket.cs` builds an **open, tapered cylinder** entirely in code:
- top ring + bottom ring + side-wall quads + a bottom cap fan (open top),
- rendered double-sided and semi-transparent so the liquid inside stays visible,
- `[ExecuteAlways]`, so it rebuilds live while tuning in the Inspector.

It also drives the matching data automatically (`MatchFluid`):
- the SPH analytic container (radius / half-height / centre) is derived from the real
  bucket dimensions minus a wall inset, so the fluid always fits the walls,
- the particle count auto-fills the bucket to `fillFraction`,
- the rope attach point is set to the bucket's top-rim centre.

**Studied default proportions** (one click via the `Apply Recommended Shape` context menu):

| Field | Value |
|---|---|
| Top Radius | 0.50 |
| Bottom Radius | 0.38 |
| Height | 1.00 |
| Segments | 32 |
| Fill Fraction | 0.90 |

## 2. Hanging rope + suspension point

`Rope.cs` (`[ExecuteAlways]`) draws a thin cylinder from the fixed suspension (pivot)
point to the bucket's top-rim attach point — pure rendering, no physics:
- the rope length stays constant and the rope never tilts oddly (a rigid, inextensible
  rope, matching the current pendulum),
- a visible suspension ball (`RopeAnchor`) is pinned at the pivot; its size (0.12) is
  now enforced from code so it is always clean and consistent,
- rope thickness default is 0.05.

`Bucket.cs` exposes `PivotWorld` (authored position + `pivotLift`) and
`RopeAttachWorld` (`TransformPoint(ropeAttachLocal)`), and `BucketTilt.cs` reads the same
`PivotWorld`, so the rope stays attached to the rim throughout the swing and the bucket
tilts along the real rope direction.

## 3. Realistic settled fluid (no visible compression)

Weakly-compressible SPH compresses under gravity for the first frames, so the fluid
looked like it "spawned big, then collapsed". Fix, applied to every `SphFluid`:
- **Pre-settle (warm-up):** at spawn, with the hole closed, the solver is stepped
  silently for `warmupSteps` (150) so the fluid reaches its rest shape before the first
  visible frame; particle velocities are then zeroed.
- **Solver tuning for the bucket fluid:** `stiffness = 8`, `timeStep = 0.003`,
  `maxSubSteps = 6` — incompressible enough to hold its shape, stable while swinging.

Result: the bucket shows a settled, filled body of liquid from the first frame.

## 4. Code cleanup

The old "flat liquid surface disc" approach (Phase-1 Steps 0–3) is fully superseded by
the SPH + procedural-bucket system and was removed:

**Deleted files**
- `Assets/Scripts/SpScr.cs` (+ meta) — unused test mover script (0 scene references).
- `Assets/Scripts/Liquid/LiquidController.cs` (+ meta) — old level-surface keeper
  (0 scene references).
- `Assets/Meshes/LiquidDisc.asset` (+ meta) — orphaned old disc mesh (0 references).

**Editor tool (`LiquidSurfaceTools.cs`)**
- Removed menus `Step 0 – Inspect Bucket`, `Step 1 – Create Liquid Surface`,
  `Step 2 – Add Liquid Controller`, `Step 3 – Apply Liquid Shader`.
- Removed the now-unused helpers `TryGetBounds` and `CreateOrLoadDiscMesh` and the old
  disc tunable constants.
- Kept everything still in use: SPH / tank / canvas / UI / rope / procedural-bucket
  menus and `CreatePaintMaterial` (used by the SPH fluid and the tank).

Verified: no dangling references, braces balanced, project compiles clean, scene runs.

---

## Files touched
- `Assets/Scripts/Liquid/ProceduralBucket.cs` — recommended-shape defaults + context menu
- `Assets/Scripts/Liquid/Rope.cs` — thickness default + code-driven anchor size
- `Assets/Scripts/Liquid/Editor/LiquidSurfaceTools.cs` — removed old Step 0–3 tooling
- Deleted: `SpScr.cs`, `LiquidController.cs`, `LiquidDisc.asset` (+ metas)
