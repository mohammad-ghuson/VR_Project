# Milestone Report — M2: Liquid Inside the Bucket (SPH)

**Project:** Swinging Paint Bucket (VR) · Unity 6000.4.10f1 · URP 17.4
**Milestone:** M2 — represent the paint inside the bucket using Smoothed Particle Hydrodynamics
**Status:** Complete (physics). Visual = instanced particles (smooth-surface polish deferred to M7).

---

## 1. Goal

Represent the paint inside the bucket as a **real fluid simulation** (SPH), implemented
**from scratch** in C# — no physics engine (no Rigidbody/Collider/Joint), no ready-made
fluid packages. The fluid must slosh realistically as the bucket swings.

---

## 2. Files

| File | Role |
|---|---|
| `Assets/Scripts/Liquid/SphFluid.cs` | The full SPH solver (spawn, neighbors, density/pressure, viscosity, moving container, instanced rendering) |
| `Assets/Shaders/LiquidPaint.shader` | Hand-written URP lit shader (now GPU-instancing capable) used to render particles |
| `Assets/Scripts/Liquid/Editor/LiquidSurfaceTools.cs` | Editor menu to create the SPH object wired to the bucket |
| `Assets/Scripts/Liquid/BucketTilt.cs` | Tilts the bucket while swinging (M1) — drives the sloshing |

Superseded (earlier surface approach, pending cleanup): `LiquidController.cs`, the disc
portion of `LiquidSurfaceTools.cs`.

---

## 3. How we built it — the steps we went through

The work was split into six small, individually tested steps.

- **Step A — Particles, gravity, boundary.** Spawn N particles in a grid; apply gravity;
  integrate (semi-implicit Euler); bounce off an analytic container (a cylinder
  approximating the bucket interior, sized from the bucket's render bounds). No
  particle interaction yet → behaves like falling grains.
- **Step B — Neighbor search (spatial hash).** A uniform grid (cell size = smoothing
  radius `h`) so each particle only checks the 27 nearby cells instead of all particles.
  Verified via a Console log of average neighbor count.
- **Step C — Density + pressure.** Poly6 kernel for density, equation-of-state pressure
  `p = k(ρ − ρ0)`, Spiky-gradient pressure force. Particles stop overlapping and cohere
  into an incompressible blob. `ρ0` is auto-estimated from the initial packing.
- **Step D — Viscosity.** Viscosity force via the Laplacian kernel; `μ` makes the fluid
  watery (low) or thick paint (high). Also adds stability.
- **Step E — Couple with the swinging bucket.** The container becomes an *oriented*
  cylinder that follows the bucket's moving/tilting transform. Wall velocity (linear +
  angular) is estimated each frame, and particle velocity is reflected **relative to the
  moving wall** — so the swinging bucket drags the fluid and sloshing emerges naturally.
- **Step F — Rendering + performance.** Replaced one-GameObject-per-particle with a single
  `Graphics.RenderMeshInstanced` call (GPU instancing; the shader got instancing support).
  Added an on-screen FPS + particle-count readout.

---

## 4. The SPH model (Müller et al. 2003)

Per particle `i`, each substep:

1. Density: `ρ_i = Σ_j m · W_poly6(r_ij, h)`
2. Pressure: `p_i = max(0, k · (ρ_i − ρ0))`
3. Pressure force: `f_press_i = −Σ_j m · (p_i + p_j)/(2 ρ_j) · ∇W_spiky(r_ij, h)`
4. Viscosity force: `f_visc_i = μ · Σ_j m · (v_j − v_i)/ρ_j · ∇²W_visc(r_ij, h)`
5. Acceleration: `a_i = g + (f_press_i + f_visc_i)/ρ_i`
6. Integrate: `v += a·dt; x += v·dt`, then resolve the (moving) container boundary.

Kernels (coded directly):
- Poly6:   `315/(64π h⁹) · (h² − r²)³`
- Spiky∇:  `45/(π h⁶) · (h − r)²` (direction along the particle offset)
- Visc∇²:  `45/(π h⁶) · (h − r)`

A fixed timestep with capped substeps keeps it stable; a `maxSpeed` clamp guards against blow-ups.

---

## 5. Complexity — and how we reduced it

**The cost driver:** each particle needs its neighbors twice (density, then forces).

- **Naïve (every particle vs every particle):** `O(n²)`.
  For 1000 particles that is ~1,000,000 distance tests per substep — and we run several
  substeps per frame. This does not scale.
- **What we implemented — spatial hash grid:** each particle is bucketed into a grid cell
  of size `h`; neighbors can only be in the 3×3×3 block of cells around it. Average
  neighbor count `k` is roughly constant, so cost becomes:
  - `BuildGrid` = `O(n)`
  - `ComputeDensityPressure` = `O(n·k)`
  - force loop = `O(n·k)`
  - **Total per substep = `O(n·k) ≈ O(n)`** — the standard, optimal SPH complexity.

**How much we reduced it:** from `O(n²)` to `O(n)`. At 1000 particles that is the
difference between ~1,000,000 and ~tens-of-thousands of interaction tests per substep.

**Can we go lower?** Not in big-O — every particle must be touched at least once, so `O(n)`
is the floor. The remaining gains are constant-factor:
- Build the neighbor list **once per substep** and reuse it for both density and forces
  (currently computed twice) → roughly halves the work.
- Precompute kernel constants (avoid repeated `Mathf.Pow`); use `sqrMagnitude`.
- Fewer substeps / larger timestep where stability allows.
- For thousands of particles: move the solver to a **GPU compute shader** (same `O(n)`
  math, massively parallel) — the planned upgrade path.

---

## 6. How to test

1. Open `SampleScene`, let Unity compile (Console clean).
2. Select `Bucket`, run `Tools → Liquid → SPH - Step A - Create SPH Fluid` (if not present).
3. Enable `Bucket` + `BucketTilt`; use gentle motion (`omega ≈ 1.5`, `thetaMax ≈ 30`).
4. Press Play; watch from the Scene view, looking into the bucket from above.
5. Expected: paint particles fill the bucket bottom, cohere as a fluid, and **slosh/wave**
   as the bucket swings, settling when it stops.
6. Tuning knobs on `SphFluid`: `viscosity` (watery↔thick), `stiffness` (incompressibility),
   `particleCount`, `timeStep`, `smoothingRadius`, `maxSpeed`. On-screen HUD shows FPS.

---

## 7. Constraints honored
- All logic hand-written (kernels, neighbor search, integration, boundary). No physics
  engine, no colliders, no fluid packages. The particle mesh is generated procedurally.
- Rendering uses standard APIs (mesh + instancing) and our own URP shader — the allowed
  rendering platform, not a simulation shortcut.

## 8. Known limitations / next
- Visual is discrete spheres; a smooth fluid surface is optional polish (M7).
- Next milestone **M3**: open a hole so particles exit the bucket → paint outflow, feeding
  M4 (trails on the canvas).
