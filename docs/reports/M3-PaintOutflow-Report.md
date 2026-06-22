# Milestone Report — M3: Paint Outflow From the Bucket

**Project:** Swinging Paint Bucket (VR) · Unity 6000.4.10f1 · URP 17.4
**Milestone:** M3 — paint leaves the bucket through a hole and falls while the bucket swings
**Builds on:** M2 (SPH fluid). All logic hand-written; no physics engine, no ready-made packages.
**Status:** Complete.

---

## 1. Goal

Connect the in-bucket SPH fluid to the PDF's core idea: paint must **exit the bucket**
through a hole and fall, with the outflow governed by physics (PDF §6b: outflow depends on
**viscosity, hole diameter, bucket speed**). We changed only the boundary/exit handling —
the SPH physics from M2 is untouched.

---

## 2. Files

| File | Role |
|---|---|
| `Assets/Scripts/Liquid/SphFluid.cs` | All M3 logic (hole, exit, free-fall, despawn, flow measurement) |

No new physics components, colliders, or packages were introduced.

---

## 3. How we represented the hole

The bucket interior is modelled (since M2) as an **oriented cylinder** that follows the
bucket: a center point `worldContainerCenter`, an axis `worldUp` (= the bucket's up, so it
tilts with the bucket), a `containerRadius`, and a half-height.

The hole is **not a separate object** — it is a region of the cylinder's **bottom cap**:

- `holeOpen` — master on/off for draining.
- `holeDiameter` — the hole's diameter in world units, centered on the cylinder axis.

A particle is "over the hole" when its distance from the axis (its radial distance in the
bottom plane) is less than the hole radius:

```
overHole = holeOpen && radial < holeDiameter * 0.5
```

Because the hole is defined in the cylinder's oriented frame, it **tilts and moves with the
bucket** automatically — paint always exits through the bucket's actual bottom, in whatever
direction the bucket is currently pointing.

---

## 4. How the paint exits (step by step)

### M3.1 — Let paint pass through the hole
The boundary resolver was reworked so that:
- The **side wall** only pushes particles back when they are within the bucket's height
  (between bottom and top). Below the floor there is no wall, so drained paint is not
  trapped in an invisible column.
- The **bottom cap** bounces particles normally, **except** when they are `overHole` — those
  are allowed to pass straight through and leave the bucket.

Result: closing the hole keeps the paint in (M2 behavior); opening it starts a drain.

### M3.2 — Drained paint becomes free-falling
- The moment a particle passes through the hole it is flagged **`escaped`**.
- Escaped particles **skip the container boundary entirely** — they obey only gravity and
  SPH interaction with their neighbors, so they fall as a natural paint stream.
- This also fixed a bug: before the flag, a drained particle that drifted sideways from the
  hole got yanked back up to the floor. Now, once escaped, it is never pulled back.
- A **kill-plane** `despawnBelowY` bounds the cost: escaped paint that falls below it is
  marked **`dead`** — frozen, hidden, and removed from the grid/neighbor search and from the
  simulation loop (there is no canvas yet to catch it; that is M4).

### M3.3 — Outflow is physical and measured
We did **not** add an artificial throttle. Outflow emerges from the physics:
- **Hole diameter** ↑ → more particles fit through → faster drain.
- **Viscosity** ↑ → fluid resists flow → slower drain.
- **Bucket speed** (swinging) → walls shove paint toward/through the hole → flow varies.

To make this observable and to feed future reports (M6), we measure:
- `drainedCount` — total particles that have left the bucket.
- `inBucketCount` — particles still inside.
- `flowRate` — drained particles per second (smoothed).

These are shown live in the on-screen HUD.

---

## 5. Important detail — exit velocity (why it looks right when swinging)

A particle keeps its velocity at the moment it exits. While the bucket swings, the moving
walls have already imparted the swing's momentum to the paint (M2's wall-relative bounce),
so paint is **flung out along the swing direction** and then arcs down under gravity — exactly
the motion that later draws curved trails on the canvas (M4).

---

## 6. Parameters added (exposed for the future UI, M5)
`holeOpen`, `holeDiameter`, `despawnBelowY` — alongside existing `viscosity`,
`smoothingRadius`, `stiffness`, `particleCount`, etc.

---

## 7. How to test

1. Open `SampleScene`, let Unity compile (Console clean).
2. Select `SPHFluid`. For a clear view, disable `Bucket`/`BucketTilt` and aim the Scene
   camera from the **side** (X axis); enable them again to see swing-driven outflow.
3. Enable `Hole Open` and press Play.
4. Expected: a paint stream falls straight out of the bottom hole and disappears below
   `despawnBelowY`; frame rate stays stable.
5. Verify the flow law with the HUD (`drained` / `flow/s`):
   - bigger `Hole Diameter` → higher flow;
   - higher `Viscosity` → lower flow;
   - enabling the swing changes the flow as the bucket shakes paint out.

---

## 8. Constraints honored
- Only boundary/exit math was added; SPH physics unchanged. No Rigidbody/Collider/Joint,
  no fluid packages. The hole is a pure math test against the oriented cylinder.

## 9. Known limitations / next
- Falling paint currently despawns (no canvas yet).
- Air resistance / wind on the falling paint is not modelled (gravity only) — can be added
  later (PDF environment inputs).
- Visual is discrete spheres; smooth surface is optional polish (M7).
- **Next: M4 — the canvas.** Drained particles that hit a canvas leave colored marks that
  accumulate into the final artwork (the PDF's main deliverable).
