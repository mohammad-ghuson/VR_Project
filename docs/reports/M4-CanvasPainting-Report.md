# Milestone Report — M4: Painting on the Canvas

**Project:** Swinging Paint Bucket (VR) · Unity 6000.4.10f1 · URP 17.4
**Goal (PDF core + supervisor request):** paint leaving the bucket hits a canvas and leaves
colored trails that accumulate into the artwork.
**Status:** Complete (M4.1–M4.3). Pure C# math; no colliders, no physics engine.

---

## 1. Files

| File | Role |
|---|---|
| `Assets/Scripts/Liquid/PaintCanvas.cs` | The canvas: writable texture, analytic-plane hit test, soft stamps, connected strokes |
| `Assets/Scripts/Liquid/SphFluid.cs` | Escaped paint that reaches the canvas leaves a mark and is consumed |
| `Assets/Scripts/Liquid/Editor/LiquidSurfaceTools.cs` | Menus: create canvas (M4.1), link bucket→canvas (M4.2) |
| `Assets/Meshes/CanvasQuad.asset`, `Assets/Materials/PaintCanvas.mat` | Canvas quad + material (generated) |

---

## 2. How we built it

### M4.1 — The canvas
- A flat quad (built by us) lying in local XZ with a writable `Texture2D` (default 1024²),
  filled with a base "paper" color. Placed horizontally under the bucket.
- The canvas is described **analytically**: center, normal (its up axis), and two in-plane
  axes — so we can test paint hits with plain math (no collider).

### M4.2 — Paint leaves a mark
- Each escaped (free-falling) paint particle is tested against the canvas plane: when it
  reaches the plane (within the particle radius) **and** its projection lies inside the
  canvas bounds, we convert the hit point to texture UV, stamp a colored splat, and consume
  the particle. Paint that misses the canvas keeps falling and despawns.
- Splats are batched into a pixel buffer and uploaded to the texture **once per frame**.

### M4.3 — Polish (soft edges + connected strokes)
- **Soft edges:** each splat is full color at its center and fades to transparent at the rim,
  **blended** over what's already there — so layers build up like real paint instead of hard
  discs. Controlled by `edgeSoftness`.
- **Connected strokes:** if a new hit is close to the previous one, we stamp a thick soft
  **line** between them instead of a separate dot, so the moving impact point draws a
  continuous brush stroke (the classic swinging-bucket streak). Far jumps stay as dots.

---

## 3. How to test
1. `Tools → Liquid → Canvas - M4.1 Create Canvas`
2. `Tools → Liquid → Canvas - M4.2 Link Bucket Paint To Canvas` (in EDIT mode), then Ctrl+S.
3. Give the bucket a wide swing (`l ≈ 4`, `thetaMax ≈ 45`, `omega ≈ 1.5`).
4. Press Play → paint pours out, hits the canvas, and draws accumulating colored trails.
- Tuning: `SphFluid.paintColor`, `SphFluid.splatRadius` (stroke width), `PaintCanvas.edgeSoftness`.

---

## 4. Notes / next
- Reduce `SphFluid.particleCount` (e.g. 500) for smooth FPS; disable the side tank while
  demoing the painting.
- Multi-color paint and a "clear/save canvas" control fit naturally into **M5 (UI)** — next
  per the supervisor (drawing first, then interfaces).
- Optimization noted by the supervisor (compute neighbors once per step) still pending.
