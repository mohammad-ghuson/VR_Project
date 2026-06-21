# Phase 1 (Revised) — Liquid Inside the Bucket via SPH

> Status: **PLAN — awaiting approval. No SPH code written yet.**
> Technique: **Smoothed Particle Hydrodynamics**, implemented from scratch in C# (CPU).
> Constraint: no physics engine, no ready-made fluid packages. We write the solver ourselves.

---

## 1. Why this replaces the surface approach

The earlier shader-surface (flat disc that stays horizontal) is a *visual trick*, not a fluid
simulation. SPH is a **real particle-based fluid simulation**: many small particles carry mass,
interact through smoothing kernels, and produce sloshing/waves/pouring **emergently** from physics.

### What we keep / drop
| Asset | Fate |
|---|---|
| `LiquidSurfaceTools` disc, `LiquidController` (keep-horizontal) | **Retired** once SPH works (kept disabled until then) |
| `LiquidPaint.shader` | **Reused** to render particles |
| `BucketTilt` (bucket swing/tilt) | **Kept** — the moving bucket drives the sloshing |

---

## 2. The SPH model we will implement (Müller et al. 2003)

Per particle `i` each frame:

1. **Density**: `ρ_i = Σ_j m · W_poly6(r_ij, h)`
2. **Pressure** (equation of state): `p_i = k · (ρ_i − ρ0)`
3. **Pressure force**: `f_press_i = −Σ_j m · (p_i + p_j)/(2 ρ_j) · ∇W_spiky(r_ij, h)`
4. **Viscosity force**: `f_visc_i = μ · Σ_j m · (v_j − v_i)/ρ_j · ∇²W_visc(r_ij, h)`
5. **Gravity**: `f_grav_i = ρ_i · g`
6. **Integrate** (semi-implicit Euler): `a = f/ρ; v += a·dt; x += v·dt`
7. **Boundary**: reflect velocity (with damping) when a particle leaves the container.

Smoothing kernels (we code these directly):
- Poly6:    `W = 315/(64π h⁹) · (h² − r²)³`           (density)
- Spiky∇:   `∇W = −45/(π h⁶) · (h − r)² · r̂`           (pressure)
- Visc∇²:   `∇²W = 45/(π h⁶) · (h − r)`                (viscosity)

All of these map to the PDF's required inputs: gravity, viscosity (لزوجة الطلاء),
paint amount (particle count / volume), and later flow-out.

---

## 3. The container problem (important)

SPH needs the bucket interior as a collision boundary. But `Bucket.fbx` is binary and
`isReadable = 0`, so we cannot read its mesh. We will **approximate the interior analytically**
(a vertical cylinder, or a slightly tapered cone matching the bucket), measured from the bounds
we already got in Step 0. Particles collide against this math shape — fully self-implemented,
no colliders. The shape is exposed as parameters (radius, bottom Y, height) for tuning.

---

## 4. Step-by-step plan (small, testable)

### Step A — Particles: spawn + gravity + boundary (no interaction yet)
- `Particle` data (position, velocity) in plain arrays.
- Spawn N particles in a grid inside the container.
- Apply gravity, integrate, bounce off the analytic container.
- Render each particle as a small sphere via a **hand-built sphere mesh** + `Graphics.DrawMeshInstanced`.
- **Test:** particles fall and pile up at the bucket bottom (like sand, no cohesion yet).

### Step B — Neighbor search (spatial hash grid)
- Uniform grid keyed by cell, so each particle only checks nearby particles (O(n) not O(n²)).
- **Test:** simulation stays fast as N grows; optional debug count of neighbors.

### Step C — Density + pressure (core incompressibility)
- Poly6 density, equation-of-state pressure, Spiky-gradient pressure force.
- **Test:** particles stop overlapping, form an incompressible blob with a roughly flat top.

### Step D — Viscosity (paint feel)
- Viscosity force via the Laplacian kernel; expose `μ`.
- **Test:** low μ = watery, high μ = thick paint.

### Step E — Couple with the swinging bucket
- Container follows the bucket's moving/tilting transform, so the walls push the fluid.
- **Test:** swing the bucket → fluid **sloshes and waves naturally** (the goal).

### Step F — Rendering + parameters + performance
- Polish rendering (reuse `LiquidPaint.shader`; optional fluid-surface later).
- Expose all parameters; measure performance; tune particle count for VR.
- **Test:** convincing fluid at an acceptable frame rate.

---

## 5. Key parameters (exposed for tuning)
`particleCount`, `smoothingRadius h`, `restDensity ρ0`, `stiffness k`, `viscosity μ`,
`particleMass m`, `gravity g`, `timeStep dt`, `boundaryDamping`, container `radius/height/bottom`.

---

## 6. Risks & honesty
- **Performance (VR):** CPU SPH is heavy. We start at a few hundred particles and measure.
  If too slow for VR, the upgrade path is a GPU compute-shader solver (same math).
- **Stability:** SPH can blow up with bad `dt`/`k`/`h`. We tune conservatively and may
  sub-step the integration.
- **Container accuracy:** analytic shape ≠ exact bucket mesh; we tune it to look right.
- **Rendering as spheres** first; a smooth fluid surface (metaballs/screen-space) is a
  larger separate effort if desired.

---

## 7. Decision recorded
- SPH is a **mandatory requirement** (confirmed 2026-06-19).
- Implementation: **CPU/C# first** (recommended), GPU compute as a later upgrade if needed.

## 8. Long-term vision (how SPH serves the PDF)
The PDF's core deliverable is the **painting on the canvas**, produced by paint **flowing out**
of the bucket. So we design the SPH system so the SAME particles that represent paint inside the
bucket later **exit the hole, fall, hit the canvas, and leave marks**. SPH is not spent on a
static in-bucket liquid; it directly produces the required output (trails / final artwork).
Step A builds the particle foundation common to both uses.
