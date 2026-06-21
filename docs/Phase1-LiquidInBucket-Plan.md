# Phase 1 — Liquid Representation Inside the Bucket (RHC)

> Status: **PLAN — awaiting approval. No code written yet.**
> Project: Swinging Paint Bucket (VR) — Unity 6000.4.10f1, URP 17.4

---

## 1. Scope of This Phase

Represent the paint/liquid **inside** the bucket as a convincing 3D visual.
This phase is **visual + lightweight physics only**. It does **not** include paint
flowing out, drawing on the canvas, or full fluid simulation (those are later phases
per the project PDF).

What "done" looks like for this phase:

- The bucket visibly contains paint up to an adjustable **fill level**.
- The paint surface **stays horizontal in world space** while the bucket tilts/swings.
- Paint has a configurable **color** and a clean **surface + rim (meniscus)** look.
- (Optional upgrade) The surface shows a subtle **slosh/tilt** reacting to bucket motion.
- All key values are exposed as **parameters** in the Inspector for later tuning.

Out of scope here (later phases):
- Paint outflow / flow rate / nozzle diameter.
- Trails on the canvas.
- Real particle/SPH fluid.

---

## 2. Current State (relevant to this phase)

- `Assets/Bucket.cs`: kinematic pendulum + circular-motion toggle. Moves
  `transform.position` directly using hand-written equations. **Uses a fixed
  `dt = 0.01` instead of `Time.deltaTime`** → motion speed depends on frame rate.
- `Bucket.fbx`: the bucket mesh (interior geometry needs verifying).
- No shaders, no liquid assets exist. This is built from scratch.

> Note: `Bucket.cs` currently only changes position, **not rotation**. A real
> swinging bucket should also tilt. The liquid system is designed to work whether
> or not the bucket rotates, but tilt is what makes the liquid effect convincing.

---

## 3. Recommended Technique (and why)

| Technique | Visual quality | VR performance | Complexity | Verdict |
|---|---|---|---|---|
| **Shader-based liquid surface on a mesh** | Very good | Excellent (cheap) | Medium | **Chosen** |
| Dynamic sloshing (wave/spring mesh) | High | Good | High | Upgrade later (Step 5) |
| Particle System (Shuriken/VFX) | Good for pouring, weak for a still surface | Medium–heavy | Medium | For outflow only (later phase) |
| SPH / real fluid particles | Highest | Too heavy for VR | Very high | Rejected |

### Decision: shader-based liquid surface (hybrid, upgradeable)

The liquid is **not** a physics simulation — it is a visual trick:

- A simple mesh (capped cylinder/disc) placed inside the bucket represents the paint volume.
- A shader fills it up to a **fill plane**; the cap fragment is clipped at the world-space
  liquid height so the visible surface always reads as horizontal.
- Color, transparency, depth tint, and a Fresnel rim give the "paint" look.

Why this is best for this project:

1. **Cheap on VR** — holds 72–90 FPS per eye; just a few extra fragments.
2. **~90% of the visual conviction** for a fraction of the cost.
3. **Upgradeable** — the same mesh later gets slosh waves without a rewrite.
4. **Integrates with the existing pendulum** — the surface stays level while the
   bucket moves, which alone sells the "real liquid" feeling.
5. Directly supports PDF inputs: **paint amount** (fill level) and **paint color**,
   and leaves a clean hook to **decrease volume** when outflow is added later.

---

## 4. Step-by-Step Plan (small, testable)

Each step is independently testable in the Unity Editor before moving on.

### Step 0 — Preparation & geometry check
- Inspect `Bucket.fbx`: confirm it is hollow / has an interior, find the inner radius,
  inner bottom Y and rim Y (the liquid must sit inside these bounds).
- Decide the bucket's local "up" axis and pivot.
- Small fix (separate, optional but recommended): make `Bucket.cs` motion use
  `Time.deltaTime` instead of fixed `dt`, so motion is frame-rate independent.
- **Test:** open the scene, confirm bucket geometry and a sensible interior reference point.
- **Expected:** documented inner dimensions; bucket still moves as before.

### Step 1 — Liquid surface mesh
- Add a `LiquidSurface` child object inside the bucket: a capped cylinder/disc sized
  to the inner radius, positioned at `fillLevel` height.
- Plain material for now.
- **Test:** with the bucket still, the disc fills the bucket cross-section and looks
  like a flat paint surface.
- **Expected:** a visible, correctly sized liquid disc; no clipping through walls.

### Step 2 — Keep the surface horizontal in world space
- Add `LiquidController` script. While the bucket tilts, the liquid surface stays
  horizontal in world space (compensate the parent's rotation, keep world-up).
- **Test:** manually rotate the bucket in the Scene view → the surface stays level.
- **Expected:** surface never tilts with the bucket; it reads as real liquid.

### Step 3 — Liquid shader (look)
- Build a Shader Graph (URP) for the surface: base color, transparency/opacity,
  depth-based tint (darker deeper), and a Fresnel rim/meniscus at the contact line.
- Expose color and opacity as material properties.
- **Test:** tweak color/opacity in the material; verify it looks like paint, not glass.
- **Expected:** convincing paint surface under the scene lighting.

### Step 4 — Couple with pendulum motion
- Run the scene with `Bucket.cs` swinging; confirm the liquid stays level and stable
  during the full swing and circular modes.
- **Test:** play mode, watch a full pendulum cycle and circular mode.
- **Expected:** stable, convincing liquid throughout the motion; no jitter/clipping.

### Step 5 (optional upgrade) — Fake sloshing
- Add a subtle surface tilt proportional to the bucket's acceleration, plus a light
  damped wave, so fast swings show a slosh. Still no real fluid.
- **Test:** swing fast vs slow; verify slosh magnitude tracks speed and settles when still.
- **Expected:** added realism without performance cost; tunable intensity.

### Step 6 — Parameters & polish
- Expose in the Inspector: `fillLevel` (paint amount), `paintColor`, opacity,
  optional viscosity-look and slosh intensity. Add a public hook to change fill
  level at runtime (for the future outflow phase).
- Measure VR performance.
- **Test:** change each parameter, confirm live effect; check frame timing.
- **Expected:** fully parameterized, VR-friendly liquid; ready to connect to outflow later.

---

## 5. Risks & Things to Watch

- **Bucket interior geometry:** if `Bucket.fbx` is solid or has no interior, the
  liquid will clip or be hidden — Step 0 must confirm this first.
- **Transparency sorting (URP):** transparent liquid can sort incorrectly against the
  bucket walls; may need render-queue/depth tuning.
- **VR stereo & near clipping:** liquid seen up close in VR must not show backface or
  near-plane artifacts.
- **Bucket rotation:** the effect is far more convincing if the bucket tilts. Current
  `Bucket.cs` only translates. We may want to add rotation (a separate decision).
- **Frame-rate dependence:** existing `dt` issue can make motion (and any motion-driven
  slosh) inconsistent — recommend the Step 0 fix.

---

## 6. What I Need From You

1. Approve the **shader-based** technique (or request a different one).
2. Confirm fidelity target: start at **Step 1–4 (visual + horizontal surface)** and
   treat **Step 5 (slosh)** as an optional upgrade — recommended.
3. Permission to apply the small Step 0 fix (`Time.deltaTime`) — or keep motion as-is for now.

Once approved, I will detail **Step 0 + Step 1 only** (exact files, code, test
checklist, expected result, pitfalls) for your sign-off before any implementation.
