# M4 — Painting on the Canvas

> Status: **PLAN — awaiting approval. No code yet.**
> The PDF's main deliverable: falling paint hits a canvas and leaves trails that
> accumulate into the final artwork. Builds on M3 (paint that exits and falls).

---

## 1. Concept

A flat **canvas** sits below the swinging bucket. When a paint particle (an "escaped"
particle from M3) reaches the canvas, it leaves a colored **mark** at the hit point. Marks
accumulate over time into a painting. As the bucket swings, the moving stream draws curved
**trails** — exactly the swinging-bucket art technique.

We keep our constraints: **no colliders**. The canvas is an analytic rectangle (a plane +
bounds) and we detect hits with our own math. Painting is done by writing into a texture
(standard rendering, not physics).

---

## 2. How it will work

- **Canvas = analytic plane + quad.** Center `C`, normal `N`, two in-plane axes (`u`,`v`)
  with half-width/half-height. A quad mesh (built by us) shows it, textured with a writable
  `Texture2D` that starts blank.
- **Hit detection.** For each falling particle, track its signed distance to the plane
  `d = dot(p − C, N)`. When it reaches/crosses the plane (`d ≤ particleRadius`) AND its
  projection lies within the rectangle bounds → it's a hit.
- **Leave a mark.** Convert the hit point to texture UV, stamp a small colored splat
  (filled circle) into the texture; then the particle is consumed (marked dead).
- **Accumulate.** The texture is never cleared during a run → trails build up into the
  final artwork.

This naturally produces the PDF outputs: live trails (#2), final painting (#3). Saving the
image, spread-area and report come later (M6).

---

## 3. Steps (small, testable)

### M4.1 — Canvas surface
- A `PaintCanvas` component: builds a quad (our mesh), holds a writable `Texture2D`,
  exposes size (width/height), orientation (horizontal or tilted), and base/surface color.
- Editor menu to create the canvas positioned below the bucket.
- **Test:** a blank canvas appears below the bucket at the chosen size/orientation.

### M4.2 — Paint hits leave marks
- `SphFluid` gets an optional `PaintCanvas` reference. When an escaped particle reaches the
  plane within bounds → compute UV → `canvas.Splat(uv, color, radius)` → mark the particle
  dead.
- **Test:** with the bucket still and the hole open, paint falls and leaves colored dots on
  the canvas directly under the hole.

### M4.3 — Trails, color & brush
- Accumulate marks (no clearing); paint **color** parameter; splat **radius** tied to
  particle size / paint amount; soft edges so dots merge into smooth trails.
- Multi-color support (basic): allow the paint color to be set (foundation for M5 UI).
- **Test:** swing the bucket with the hole open → curved colored trails accumulate into a
  pattern, like real swinging-bucket art.

---

## 4. Parameters added (for the future UI, M5)
Canvas: `width`, `height`, `orientation` (flat/tilted angle), `surfaceColor`,
`textureResolution`. Paint: `paintColor`, `splatRadius`.

---

## 5. Out of scope (later milestones)
- Save image to disk, spread-area, trail count, report → **M6**.
- Surface *material types* (canvas/wood/metal/paper) beyond a base color → visual, **M6/M7**.
- Air resistance / wind on the falling paint → later (PDF environment inputs).
- Smooth fluid look → **M7**.

---

## 6. Risks
- **Texture write performance:** stamping per hit then `Apply()` once per frame keeps it
  cheap; batch all hits in a frame before applying.
- **Missed hits at high speed:** a fast particle could pass through the plane in one step;
  mitigate with the signed-distance crossing test (not just "below the plane").
- **Canvas placement:** must be below the bucket's swing arc and above `despawnBelowY` so it
  catches the paint before it despawns.
