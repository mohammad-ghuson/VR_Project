# Report — Why SPH Particles Visually Overlap (and How to Control It)

**Project:** Swinging Paint Bucket (VR) · Unity 6000.4.10f1 · URP 17.4
**Purpose:** explain the observation that, after the fluid settles, the rendered particles seem
to "sink into each other" / look smaller than at spawn — and give the justification and the
possible fixes. This is **expected physics of our solver, not a bug**.

---

## 1. The observation

- **At spawn:** particles are laid out on a perfect grid with spacing equal to one particle
  diameter, so every rendered sphere looks separate and crisp (with a high particle count the
  spawn block can even stick out above the bucket rim before it falls in).
- **After settling:** the pile compacts; neighbouring spheres visually interpenetrate, which
  reads as "the particles shrank" or "merged into each other". The rendered size is actually
  constant — only the **spacing** changed.

---

## 2. Why this happens (the justification)

### 2.1 An SPH particle is not a rigid ball
A particle is a **sample point** of the fluid that carries a smoothing kernel of radius
`h = smoothingRadius = 0.2`. The sphere we draw is only a **visual marker** of radius
`particleRadius = 0.05` — four times smaller than the physical kernel. Collisions between
particles are never resolved as sphere-vs-sphere contact; there is nothing in SPH that forbids
two markers from overlapping.

### 2.2 What resists crowding is pressure, not contact
Density is measured with the Poly6 kernel and converted to pressure with an equation of state:

```csharp
// rho_i = sum_j m * W_poly6(r_ij) ;  p_i = max(0, k * (rho_i - rho0))
density[i]  = Mathf.Max(rho, 1e-5f);
pressure[i] = Mathf.Max(0f, stiffness * (density[i] - restDensity));
```

When particles crowd together, local density `ρ` rises above the rest density `ρ₀`, pressure
grows, and the Spiky-gradient pressure force pushes them apart. Pressure **limits** overlap; it
does not **forbid** it.

### 2.3 We use weakly-compressible SPH (WCSPH) with a soft stiffness
This is the standard Müller-2003 real-time model. Our `stiffness = 3` is deliberately soft: a
stiff equation of state produces huge pressure spikes that demand a much smaller timestep to stay
stable (the classic WCSPH trade-off). With a soft `k`, the fluid is allowed to compress slightly —
so under gravity the lower layers pack a little tighter than one diameter, and the rendered
spheres visibly interpenetrate. That small compression **is** the "overlap" being observed.

### 2.4 Why it looks different at spawn vs. settled
The spawn grid uses exactly one-diameter spacing (touching, not overlapping). Settling replaces
that artificial arrangement with the **gravity–pressure equilibrium**, whose natural spacing at
the bottom of the pile is slightly under one diameter. Nothing changed size; the packing changed.

**One-sentence summary for the interview:**
*"The drawn sphere is a small visual marker, not the physical particle — the physics lives in a
kernel 4× larger. Our weakly-compressible SPH allows slight density variation by design, so under
gravity the pile compacts a little and the markers overlap. It's the expected WCSPH behaviour,
not an error."*

---

## 3. How to reduce it (three options, increasing cost)

| # | Option | What it does | Trade-off |
|---|---|---|---|
| 1 | **Cosmetic:** lower `particleVisualScale` (e.g. 1.0 → 0.8) | Draws smaller spheres so they no longer visually touch | Zero physics change; purely visual |
| 2 | **Physical:** raise `stiffness` (e.g. 3 → 6–10) | Stronger pressure response → less compression, more "incompressible" look | Stiffer pressure needs a smaller `timeStep` / more `maxSubSteps` or the fluid jitters/explodes |
| 3 | **Structural:** switch solver family (PBF / IISPH) | Enforces (near-)constant density by construction — no visible compression | A different, larger solver; out of scope and unnecessary for paint |

Recommended for this project: keep the current soft WCSPH (stable, fast, looks fine for paint);
use option 1 if the overlap is visually distracting in the demo.

---

## 4. Where the relevant knobs are

| Field (in `SphFluid`) | Current | Role |
|---|---|---|
| `smoothingRadius` (h) | 0.2 | physical kernel radius (the *real* particle size) |
| `particleRadius` | 0.05 | rendered marker radius (visual only) + boundary offset |
| `particleVisualScale` | 1.0 | multiplies the rendered size only |
| `stiffness` (k) | 3 | pressure response `p = k(ρ − ρ₀)`; higher = less compression, less stable |
| `restDensity` (ρ₀) | auto | measured from the initial packing (`autoRestDensity`) |
| `timeStep` / `maxSubSteps` | 0.005 / 4 | stability budget — must shrink if `k` grows |
