# Interview Demo — Run-sheet

A step-by-step script for demoing the project to the supervisor: what to click, what to say,
and the answers to likely questions. Three pillars: **painting**, **UI**, **SPH complexity**.

---

## 0. Before the interview (setup checklist)
- Open `SampleScene`. Make sure the ⏸️ pause button is OFF and the Console "Error Pause" is OFF.
- Confirm these exist in the Hierarchy: `Bucket`, `SPHFluid`, `PaintCanvas`, `ControlPanelUI`
  (with `Panel` + `DemoPanel`), `EventSystem`.
- Set `SPHFluid.particleCount = 500` for a smooth painting demo (raise it only for the
  complexity act).
- Do a dry run once end-to-end so nothing surprises you live.

---

## Act 1 — Painting (the core deliverable)
**Goal:** show paint leaving a swinging bucket and drawing on the canvas.
1. Press Play. The bucket swings; paint streams from the hole and draws trails on the canvas.
2. Say: *"The bucket is a pendulum driven by our own equations. The paint is an SPH fluid we
   wrote from scratch — no physics engine. Particles that drain through the bottom hole become
   free-falling paint; when they reach the canvas we stamp a colored mark, and the marks build
   up into the artwork."*
3. From the left panel: pick a different **color swatch**, watch the trail change color.
4. Click **Save PNG** → point out the saved file path in the Console (`<Project>/SavedPaintings`).
5. Click **Clear Canvas** to reset.

## Act 2 — The interface (parameter control)
**Goal:** show every physical input is user-controllable live.
1. While playing, drag **Rope Length / Release Angle / Speed** → the swing changes immediately.
2. Drag **Viscosity / Hole Diameter / Splat Width** → the fluid and stroke respond.
3. Show **RGB sliders**, the **Hole Open/Close** toggle, and **Reset**.
4. Say: *"Every PDF input is exposed at runtime. The UI is standard Unity uGUI — a UI system,
   not physics — so it doesn't violate the no-physics-engine constraint. The panel is built by
   an editor tool and wired to the simulation through one script (`UIControlPanel`)."*

## Act 3 — SPH complexity (the technical highlight)
**Goal:** prove the neighbour search is O(n), and that we optimised the constant.
1. In the **"SPH Complexity"** panel (top-right): set **Particles = 3000** → **Apply**.
2. Read the live **ms** in **Grid O(n)** mode (≈ 14 ms on our machine).
3. Press **Mode** → **Brute O(n²)**. The fluid is identical, but the ms jumps (≈ 65 ms, ~4.5×).
4. Repeat at **1500**: grid time roughly halves (linear), brute time roughly quarters
   (quadratic). Say: *"Doubling n multiplies the grid cost by 2 but the brute cost by 4 — that's
   O(n) versus O(n²), measured live."*
5. Mention the optimisation: *"We compute each particle's neighbours once per step instead of
   twice, using a half stencil, cutting the constant from ~27n to ~13n. I proved the optimised
   lists are identical to a brute-force search — the self-check logs PASS — so the physics is
   unchanged, only faster."*

---

## Likely questions & answers

**Q: How did you add these commands to the Tools menu?**
Editor scripting: a `static` method with the `[MenuItem("Tools/Liquid/...")]` attribute (from the
`UnityEditor` namespace) registers as a menu command. The file lives in an `Editor/` folder, so
it compiles into an editor-only assembly excluded from the build. The methods build and wire
GameObjects programmatically (create objects, `AddComponent`, set values, `Undo`,
`MarkSceneDirty`) — automation that's repeatable and version-controlled, unlike manual drag-drop.
The created objects are saved in the scene and run without the menu at runtime.

**Q: Why not reach O(log n)? You said it's possible.**
O(n) is already the optimal order: every particle must be updated each step, so you can't do less
than linear. The true floor is Θ(k·n) with k = average neighbours (a constant). O(log n) *total*
is impossible. Trees/k-d give O(log n) *per query* but O(n·log n) *total* — worse — and because
the SPH kernel has **compact support** (zero beyond h) there's no far field for Barnes-Hut/FMM to
approximate, so they give no benefit. The only real gains are shrinking the constant (done:
27n→~13n) or parallel hardware (GPU/SIMD reduces wall-clock, not the order).

**Q: Is it really SPH from scratch?**
Yes. Müller-2003 model: Poly6 kernel for density, Spiky gradient for pressure, a viscosity
Laplacian, semi-implicit Euler with fixed sub-steps, and a uniform-grid spatial hash for
neighbours. All hand-written in C#; no fluid package.

**Q: Where's the physics engine?**
There isn't one. No Rigidbody/Collider/Joint. Boundaries are analytic (an oriented cylinder for
the bucket, an oriented box for the tank); collisions are velocity reflection against those
surfaces, computed in code.

**Q: How does the paint know it hit the canvas?**
The canvas is described analytically (centre + normal + two in-plane axes). Each falling particle
is tested against that plane; if it crosses within range and inside the bounds, we convert the hit
to a texture UV and stamp a soft splat, connecting nearby hits into continuous strokes.

**Q: How do you know the optimisation didn't change the result?**
A built-in self-check (`verifyNeighbors`) compares the cached neighbour lists against an
independent brute-force search for every particle; it logged **PASS**, i.e. identical sets.

---

## Numbers to cite
- Neighbour search @3000 particles: **Grid ≈ 14.4 ms** vs **BruteForce ≈ 65.2 ms** (~**4.5×**).
- Optimisation: **~27n → ~13n** (constant), order stays **O(n)** (optimal).
- Correctness: neighbour self-check = **PASS**.
