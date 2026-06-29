# Milestone Report — Transparent Liquid Tank (Supervisor request #3)

**Project:** Swinging Paint Bucket (VR) · Unity 6000.4.10f1 · URP 17.4
**Goal:** a transparent box on the side, filled with SPH liquid, that can be shaken so the
supervisor clearly sees the fluid sloshing inside.
**Status:** Complete (T1–T3). Reuses the existing SPH solver; pure C# math, no physics engine.

---

## 1. Files

| File | Role |
|---|---|
| `Assets/Scripts/Liquid/SphFluid.cs` | Added a **Box** container mode (alongside Cylinder) |
| `Assets/Scripts/Liquid/Shaker.cs` | Auto-oscillates the tank so the liquid sloshes |
| `Assets/Scripts/Liquid/Editor/LiquidSurfaceTools.cs` | Menus: create tank (T1), fill (T2), shake (T3), Setup Demo Scene |
| `Assets/Materials/GlassTank.mat`, `Assets/Meshes/PaintTankBox.asset` | Generated transparent box (material + mesh) |

---

## 2. How we built it (steps)

### T1 — Transparent box
- We generate a unit-cube mesh ourselves (24 verts, flat per-face normals) and a
  **transparent** URP/Lit material (alpha blend, both faces visible) so the inside is seen.
- `PaintTank` GameObject scaled to the tank size, placed on the side.

### T2 — Box boundary in SPH + fill
- `SphFluid` gained `containerShape` (Cylinder / Box). The box boundary clamps each particle
  inside the **oriented** box half-extents and bounces it off the face it crossed, reflecting
  velocity **relative to the wall** (the same mechanism that lets the bucket slosh).
- Half-extents are derived from the tank's scale; the container follows the tank transform.
- The box **top is left open** (bottom + 4 sides closed). This matters: a fully closed box
  traps pressure → instability → particles vanish. Leaving the top open keeps it stable,
  exactly like the bucket's open rim.
- Particles spawn pre-filled from the **floor up** (no falling-in mess).
- A second `SphFluid` instance (`TankFluid`, Box shape) fills the `PaintTank`.

### T3 — Shake → slosh
- `Shaker` rocks (and optionally slides) the tank with a sine wave in `Update`. Because the
  fluid runs in `LateUpdate` and reads the moving walls + their velocity, the liquid sloshes
  automatically. Dragging the tank by hand in the Scene view sloshes it too.

---

## 3. How to test

1. `Tools → Liquid → Tank - T1 Create Transparent Tank`
2. `Tools → Liquid → Tank - T2 Fill With Liquid`
3. `Tools → Liquid → Tank - T3 Add Shake`
4. (optional) `Tools → Liquid → Setup Demo Scene (positions + camera)` to frame everything.
5. Press Play → the transparent tank rocks and the SPH liquid sloshes/waves inside, visible
   through the glass.
- Tuning on `Shaker`: `rockAmplitude`, `rockSpeed`, `slide`. On `TankFluid`: `particleCount`,
  `viscosity`, `timeStep`.

---

## 4. Notes / next
- Two fluids (bucket + tank) run together; reduce particle counts or disable one if FPS drops.
- Next per supervisor: **M4 — drawing on the canvas**, then **M5 — UI**.
- Optimization to add later (supervisor note #1): compute each particle's neighbors **once
  per step** (currently twice) to roughly halve the work — uniform-grid stays O(n) overall
  (already optimal; tree methods would be O(n·log n), i.e. slower).
