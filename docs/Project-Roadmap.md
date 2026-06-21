# Swinging Paint Bucket — Master Roadmap

> The big picture for the WHOLE project, mapped to the PDF requirements.
> Each milestone is later broken into small, testable steps (our usual workflow)
> when we reach it. This file is the single source of truth for "where are we / what's next".

Legend: ✅ done · 🟡 in progress · ⬜ not started

---

## How our work maps to the PDF

The PDF defines 5 phases. The real end-goal is the **painting on the canvas**, produced
by paint flowing out of a swinging bucket, with the user controlling all physical inputs.

| PDF phase | Our milestones |
|---|---|
| 1. Study & analysis | done implicitly (we analyzed the project & PDF) |
| 2. Model design | M5 (UI) + parameter model |
| 3. Physics sim (bucket, rope, pendulum, liquid) | M1, M2, **M3 (current area)** |
| 4. Drawing (paint flow, trails, multi-color) | M4 |
| 5. Testing & tuning | M8 |

---

## Milestones

### M1 — Bucket motion ✅
- Pendulum motion + circular mode (`Bucket.cs`, pre-existing).
- Bucket tilts while swinging (`BucketTilt.cs`).
- **Status: done.**

### M2 — Liquid inside the bucket (SPH) ✅
- SPH fluid from scratch: gravity, spatial-hash neighbors, density/pressure (Poly6/Spiky),
  viscosity, moving-container sloshing, instanced rendering (`SphFluid.cs`).
- **Status: physics done.** Visual = particles (a smooth fluid surface is an optional polish, M7).

### M3 — Paint outflow from the bucket 🟡 (next)
The PDF core link: paint must **leave the bucket** through a hole.
- Define a hole (position + diameter) at the bucket bottom/side.
- SPH particles that reach the hole exit the container and become falling paint.
- Outflow rate depends on: hole diameter, paint viscosity, bucket speed (per PDF section 6b).
- **Test:** open the hole → paint streams out as the bucket swings.

### M4 — Painting on the canvas ⬜ (the main deliverable)
- A canvas object (dimensions, orientation: flat/tilted, surface type).
- When a paint particle hits the canvas → leave a colored mark (trail).
- Trails accumulate into the final artwork (render-to-texture on the canvas).
- Multi-color support.
- **Test:** swinging bucket paints visible trails/patterns on the canvas.

### M5 — Parameter UI ⬜
Expose all PDF inputs in a runtime UI so the user can experiment:
- Bucket: weight, radius, paint amount, hole diameter.
- Suspension: rope length, elasticity, attach point.
- Motion: start angle, initial velocity, direction, swing count.
- Environment: gravity, air resistance, humidity, friction.
- Paint: color, viscosity, flow speed, multi-color.
- Canvas: size, surface type, orientation.
- **Test:** changing a slider changes the simulation live.

### M6 — Outputs & reports ⬜
- Show the final painting; **save image** to disk.
- Show the values used in the experiment.
- **Compare** multiple experiments.
- Generate a **report**: inputs, motion time, number of trails, paint spread area.
- **Test:** run an experiment → save image + report; compare two runs.

### M7 — Visual polish (optional) ⬜
- Smooth fluid surface for the liquid (metaballs / marching cubes / screen-space).
- Better paint look on canvas (drips, thickness).
- **Test:** looks convincingly like liquid paint.

### M8 — Testing, tuning & performance ⬜ (PDF phase 5)
- Compare against real swinging-bucket art; tune parameters for realism.
- Measure & optimize performance for VR (particle count, optional GPU compute).
- **Test:** stable target frame rate; believable results.

### Extras (PDF section 9, optional) ⬜
- Multiple buckets at once · turn into a game/教育 app · export video/HQ image · AI-generated art.

---

## Constraints (apply to everything)
- Implement all logic ourselves (math/code). **No physics engine** (no Rigidbody/Collider/Joint),
  no ready-made fluid/asset packages. Rendering APIs and `Mathf`/`transform` are allowed.
- Target platform: VR (performance-sensitive).
- Workflow: plan → approve → implement ONE small step → test in Unity → review → next.

---

## Where we are now
- ✅ M1 (bucket motion) and ✅ M2 (SPH liquid) are complete.
- 🟡 **Next: M3 — paint outflow from the bucket.**
- Pending housekeeping: remove the superseded surface approach files
  (`LiquidController.cs`, the disc part of `LiquidSurfaceTools.cs`); push to GitHub
  after the liquid work is wrapped up.
