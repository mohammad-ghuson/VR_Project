# M3 — Paint Outflow From the Bucket

> Status: **PLAN — awaiting approval. No code yet.**
> Builds directly on the SPH fluid (M2). Connects the in-bucket liquid to the
> PDF's core: paint that **leaves** the bucket (PDF §6b: outflow depends on
> viscosity, hole diameter, bucket speed).

---

## 1. Concept

Classic swinging-bucket painting uses a **hole in the bottom of the bucket**. Paint
drains through it under pressure/gravity while the bucket swings, drawing trails below.

So M3 adds a hole at the bottom-center of the SPH container. Particles that reach the
hole are allowed to pass through (instead of bouncing off the floor), exit the bucket,
and fall freely as the paint stream. The hole follows the bucket's tilt (it's defined in
the container's oriented frame from M2).

We change only the **boundary handling** — the SPH physics (density/pressure/viscosity)
stays exactly as in M2. Outflow rate then emerges naturally from pressure, hole size,
and viscosity.

---

## 2. Steps (small, testable)

### Step M3.1 — Define the hole + let paint pass through it
- Add parameters: `holeDiameter`, `holeOpen` (toggle).
- In the bottom-boundary check: if a particle is within the hole radius of the container
  axis, **don't bounce it** — let it move below the floor.
- **Test:** with `holeOpen = false` paint stays in (M2 behavior). With it open, paint
  starts draining through the bottom hole.
- **Expected:** a stream of particles leaves the bottom; closing the hole retains the paint.

### Step M3.2 — Escaped paint becomes free-falling
- Mark a particle "escaped" once it passes below the floor through the hole. Escaped
  particles ignore the container entirely (only gravity + SPH among themselves).
- Add an optional kill-plane `despawnBelowY`: escaped particles that fall far below are
  recycled/parked, to bound compute while there's no canvas yet (M4 will catch them).
- **Test:** the stream falls below the bucket and keeps falling; frame rate stays stable.
- **Expected:** a continuous paint stream under the bucket; particle count under control.

### Step M3.3 — Outflow parameters (PDF §6b)
- Confirm/tune that outflow rate responds to: **hole diameter** (bigger = faster),
  **viscosity** (thicker = slower), and **bucket speed** (swinging shakes paint out).
- Expose these cleanly for the future UI (M5).
- **Test:** compare drain time for small vs large hole, and low vs high viscosity.
- **Expected:** intuitive, tunable flow consistent with the PDF's flow law.

---

## 3. Parameters added
`holeDiameter`, `holeOpen`, `despawnBelowY` (and reuse existing `viscosity`,
container radius/axis from M2).

---

## 4. Out of scope (later milestones)
- Trails/marks on a canvas → **M4**.
- Refilling the bucket / continuous source → not required by the PDF.
- Smooth fluid/stream surface → **M7**.

---

## 5. Risks
- **Instant dump:** a large hole + low viscosity drains everything at once. Mitigate with
  a sensible default hole size and viscosity; it is also physically correct.
- **Particles leaking around the hole edge** at high speed: handled by checking the hole
  radius against the particle's radial position with the particle radius as margin.
- **Compute from fallen particles:** addressed by the kill-plane in M3.2.
