# SPH Liquid — Study Guide (for the milestone interview)

A complete walkthrough of our fluid system in `Assets/Scripts/Liquid/SphFluid.cs`.
Goal: understand every part, and be able to defend the **complexity** and **how we reduced it**.

---

## 1. The big idea — what SPH is

**SPH = Smoothed Particle Hydrodynamics.** We represent the liquid as many **particles**.
Each particle carries: position, velocity, density, pressure. Fluid behaviour (sloshing,
incompressibility, viscosity) is **emergent** from each particle interacting with its **nearby
neighbors** — we never script "the surface" directly.

Core principle: a quantity at a point = a **weighted average of neighbor contributions**, where
the weight is given by a **smoothing kernel** `W(r, h)` that fades with distance and becomes 0
beyond the smoothing radius `h` (`smoothingRadius`).

Reference model: Müller, Charypar & Gross, *"Particle-Based Fluid Simulation for Interactive
Applications"* (2003).

---

## 2. Data layout (lines 57–90)

Parallel arrays — index `i` is the same particle across all of them (cache-friendly, far
cheaper than one GameObject per particle):

| Array | Meaning |
|---|---|
| `positions[]` | particle positions (world space) |
| `velocities[]` | particle velocities |
| `density[]` | per-particle density ρ |
| `pressure[]` | per-particle pressure p |
| `escaped[]` | has it drained out the hole? |
| `dead[]` | has it fallen below the despawn plane? (frozen + hidden) |
| `matrices[]` | per-particle transform for instanced rendering |

Neighbor structure: `grid : Dictionary<Vector3Int, List<int>>` — the spatial hash.

---

## 3. Lifecycle

- **`Start` (92):** compute the container from the bucket bounds, build the particle sphere
  mesh, defer spawning to the first frame.
- **`LateUpdate` (101):** each frame —
  1. `UpdateContainerKinematics` — move the container to follow the bucket.
  2. First frame only: `SpawnParticles` + estimate `restDensity`.
  3. **Fixed-step integration** (119–128): accumulate real time and call `Step(timeStep)` a
     fixed number of times (capped by `maxSubSteps`). Fixed `dt` = stability; tying physics to
     FPS would blow up when frame time fluctuates.
  4. `RenderParticles` + `MeasureFlow`.
- Runs in `LateUpdate` with `[DefaultExecutionOrder(60)]` so it executes **after** `Bucket.cs`
  (Update) and `BucketTilt` (LateUpdate) — it reads the bucket's final transform that frame.

---

## 4. The heart — `Step()` (262): five phases per timestep

1. **Build grid** — `BuildGrid` (431): bucket every particle into a cell of size `h`.
2. **Density + pressure** — `ComputeDensityPressure` (314):
   - density `ρᵢ = Σⱼ m · W_poly6(r)`
   - pressure `pᵢ = max(0, k · (ρᵢ − ρ0))` (compressed → higher pressure)
3. **Forces** (271–297), per particle over its neighbors:
   - pressure force (pushes overlapping particles apart → incompressibility):
     `f_press = −Σⱼ m·(pᵢ+pⱼ)/(2ρⱼ)·∇W_spiky`
   - viscosity force (averages velocities → cohesion / paint feel):
     `f_visc = μ·Σⱼ m·(vⱼ−vᵢ)/ρⱼ·∇²W_visc`
4. **Integrate** (299–306): `a = g + (f_press+f_visc)/ρ`; `v += a·dt`; `x += v·dt`
   (semi-implicit Euler), with a `maxSpeed` safety clamp.
5. **Boundary** — `ResolveBoundary` (364): bounce off the cylinder wall/floor, or drain
   through the hole.

---

## 5. The kernels (why three different ones)

| Kernel | Used for | Why this one |
|---|---|---|
| **Poly6** `315/(64πh⁹)(h²−r²)³` (318) | density | smooth & stable, well-behaved near r=0 |
| **Spiky gradient** `45/(πh⁶)(h−r)²` (290) | pressure force | strong gradient near r=0 → stops particles piling up on each other |
| **Viscosity Laplacian** `45/(πh⁶)(h−r)` (293) | viscosity | positive Laplacian that smooths velocity differences |

We code these constants directly (no libraries).

---

## 6. Coupling with the swinging bucket (sloshing)

- `UpdateContainerKinematics` (229): each frame the container's center/axis follow the bucket;
  we also estimate the wall's **linear and angular velocity**.
- `ReflectVel` (410): a particle's velocity is reflected **relative to the moving wall**
  (`vRel = v − wallVel`), so a swinging bucket drags the fluid → sloshing emerges from physics.

## 7. Outflow (M3)

- In `ResolveBoundary`, if a particle is over the bottom hole (`overHole`), it is flagged
  `escaped` and falls freely (skips the container). Below `despawnBelowY` it becomes `dead`
  (frozen, hidden, removed from the grid) to bound compute until the canvas (M4) catches it.

## 8. Rendering (Step F)

- `RenderParticles` (153): one `Graphics.RenderMeshInstanced` call draws all particles using a
  hand-built sphere mesh — no GameObject per particle. The shader (`LiquidPaint.shader`) has
  GPU-instancing support.

---

## 9. ⭐ Complexity — and how we reduced it (key interview point)

**The cost driver:** every particle needs its **neighbors** (for density and for forces). How
we find neighbors decides the complexity.

### Naïve approach → `O(n²)`
If each particle checked **every** other particle:
- per particle: `n` checks → total `n × n = n²`.
- at `n = 1000`: ~**1,000,000** checks **per timestep**, times several substeps per frame.
  Does not scale.

### What we implemented: spatial hash → `O(n)`
Neighbors are only **spatially close**. Partition space into cells of size `h`:
- `BuildGrid` (431): place each particle into its cell → **`O(n)`**.
- `GetNeighbors` (450): check only the **27 surrounding cells** (3×3×3), not all particles.

Average neighbor count `k` is roughly **constant** (independent of `n`), so:

| Function | Complexity |
|---|---|
| `BuildGrid` | `O(n)` |
| `ComputeDensityPressure` | `O(n·k)` |
| force loop | `O(n·k)` |
| **Total per step** | **`O(n·k) ≈ O(n)`** |

### How much we reduced it
From **`O(n²)` to `O(n)`**. At 1000 particles: from ~1,000,000 to ~tens-of-thousands of
checks per step. This reduction is what makes the simulation feasible at all.

### Can we go below `O(n)`?
No — every particle must be touched at least once, so `O(n)` is the algorithmic floor. We are
**at the optimum** in big-O terms.

### Remaining optimizations (lower the constant factor, still `O(n)`)
1. **Compute the neighbor list once per step.** Currently `GetNeighbors` is called **twice**
   per particle (density at line 324, forces at line 281); caching halves that work.
2. **Newton's third law symmetry.** Compute each pair force (i, j) once and apply it to both
   with opposite sign → ~half the force work.
3. **Fewer substeps / larger `timeStep`** where stability allows.
4. **GPU compute shader.** Same `O(n)` math, run massively in parallel — the upgrade path for
   thousands of particles.

---

## 10. Inspector field reference

- **References:** `bucket` (sized/followed), `particleMaterial` (render).
- **Particles:** `particleCount`; `particleRadius` (physics/collision); `particleVisualScale`
  (render only).
- **Container (auto-filled at Start, read-only):** `innerRadiusFactor`, `bottomLift`,
  `followBucket`; `containerCenter/Radius/BottomY/TopY` are computed at runtime (0 in edit mode).
- **Simulation:** `gravity`, `timeStep`, `boundaryDamping`, `maxSubSteps`.
- **Neighbor search:** `smoothingRadius` (h), `logNeighborStats`.
- **SPH forces:** `particleMass`, `restDensity` (+`autoRestDensity`), `stiffness` (k),
  `viscosity` (μ), `maxSpeed`.
- **Outflow:** `holeOpen`, `holeDiameter`, `despawnBelowY`.
- **Display:** `showStats`.

> Note: the `SphFluid` GameObject's own Transform does **not** affect the sim — particles live
> in world space and are drawn via instancing, not parented.

---

## 11. Quick interview Q&A

- **Q: Why particles, not a mesh surface?** SPH is a real fluid simulation; sloshing/outflow
  emerge from physics, not animation.
- **Q: Biggest performance idea?** Spatial-hash neighbor search: `O(n²) → O(n)`.
- **Q: Why fixed timestep + substeps?** Stability and frame-rate independence.
- **Q: Why three kernels?** Each is shaped for its job (smooth density, sharp pressure
  gradient, smoothing viscosity).
- **Q: How does it slosh?** The container follows the bucket and reflections are relative to
  the moving wall's velocity.
- **Q: What would you optimize next?** Cache neighbors once per step; pairwise force symmetry;
  then GPU compute.
