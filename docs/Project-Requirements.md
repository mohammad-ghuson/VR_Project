# Project Requirements — extracted from the official PDF
# "مشروع الحقائق الافتراضية (Swinging Paint Bucket) 2025-2026"

Permanent reference so we never lose the source again. Status legend:
✅ done · 🟡 partial · ⬜ missing · (opt) optional.
Instructor: Eng. Khaled Ismail · Final-interview weight: 18/30.

---

## §4 Required simulation INPUTS

### Bucket properties
| Requirement (PDF) | Status | Where / note |
|---|---|---|
| Bucket weight (وزن الدلو) | ⬜ | Not modeled. Honest justification available: simple-pendulum period is mass-independent; weight only matters with air drag/elastic rope. Could add as display-only or drag coupling. |
| Bucket radius (نصف قطر الدلو) | ⬜ | Container is auto-measured from the model; no user control. Could add a bucket-scale slider. |
| Paint amount inside (كمية الطلاء) | ✅ | "Particles" slider + Apply (demo panel) = amount of paint. |
| Outlet hole diameter (قطر فتحة الخروج) | ✅ | "Hole Diameter" slider. |

### Suspension properties
| Requirement | Status | Where / note |
|---|---|---|
| Rope length (طول الحبل) | ✅ | "Rope Length" slider. |
| Rope type / elasticity (نوع الحبل ومرونته) | ⬜ | Rigid-rod model only. User backlog item (visible rope + type). Justifiable as a modeling choice; elastic rope = future work. |
| Suspension point (نقطة التعليق) | 🟡 | Fixed at the bucket's authored position; moving the Bucket object moves it, but no runtime control. |

### Motion properties
| Requirement | Status | Where / note |
|---|---|---|
| Start angle (زاوية البداية) | ✅ | "Release Angle" slider. |
| Initial speed (السرعة الابتدائية) | ✅ | "Speed" (omega) slider. |
| Motion direction (اتجاه الحركة) | 🟡 | Pendulum ⇄ Circular toggle covers motion *path*; no swing-plane direction control. |
| Number of swings (عدد مرات التأرجح) | ⬜ | Not modeled — motion runs indefinitely. Could auto-stop / close hole after N swings. |

### Environment properties
| Requirement | Status | Where / note |
|---|---|---|
| Gravity value (قيمة الجاذبية) | 🟡 | "Gravity" slider drives the FLUID. Bucket equation takes omega directly (documented decision); coupling ω=√(g/l) = future. |
| Air resistance (مقاومة الهواء) | ⬜ | Not modeled. Easy: linear drag on particles + amplitude damping on the pendulum. |
| Air humidity (رطوبة الهواء) | ⬜ | Not modeled. Defensible mapping: humidity ⇒ paint stays wetter ⇒ wider/softer splats (affects splat spread/edge softness). Explicitly named in the evaluation criteria. |
| Friction (الاحتكاك) | 🟡 | "Wall Bounce" (boundary energy loss) + viscosity (internal friction). No pendulum friction/decay. |

### Paint properties
| Requirement | Status | Where / note |
|---|---|---|
| Paint colour (لون الطلاء) | ✅ | 6 swatches + RGB sliders. |
| Viscosity (لزوجة الطلاء) | ✅ | "Viscosity" slider (SPH viscosity force). |
| Flow speed (سرعة تدفق اللون) | ✅ | Emergent from hole diameter + viscosity + bucket speed — physically derived (PDF §6b lists exactly these three as the flow's drivers). |
| Multi-colour (أكثر من لون) | ✅ | Live colour switching layers multiple colours on one painting. |

### Canvas properties
| Requirement | Status | Where / note |
|---|---|---|
| Canvas dimensions (أبعاد اللوحة) | ✅ | "Canvas Size" slider (uniform). Separate width/depth = trivial extension. |
| Surface type: canvas/wood/metal/paper (نوع السطح) | ⬜ | Single generic surface. Plan: preset base colour/texture + absorption profile (splat width/softness/edge) per type. Named in evaluation criteria. |
| Canvas orientation: flat/tilted (وضعية اللوحة) | ⬜ | Plane math already supports ANY orientation (analytic plane from transform) — only a UI tilt control is missing. Cheap win. |

---

## §5 Expected OUTPUTS
| # | Requirement | Status | Where |
|---|---|---|---|
| 1 | 3-D display of bucket motion | ✅ | Core scene |
| 2 | Live trail drawing on the canvas | ✅ | M4 |
| 3 | Final painting after motion ends | ✅ | M4 |
| 4 | Save resulting image | ✅ | M5.5 → `SavedPaintings/*.png` |
| 5 | Show the values used in the experiment | ✅ | M6.1 — live HUD line + panels |
| 6 | Compare more than one experiment | ✅ | M6.3 — comparison panel (prev → current) |
| 7 | Generate a report: inputs, motion time, number of trails, colour spread area | ✅ | M6.2 — `.txt` beside each saved PNG |

→ **All seven §5 outputs are DONE** (M6 completed 2026-07-06, see `reports/M6-Outputs-Reports-Report.md`).

## §6 Physics laws
Pendulum ✅ · gravity ✅ · fluid-flow laws ✅ (from-scratch SPH) · collision/rebound ✅ (analytic walls, wall-relative reflection) · friction/air resistance 🟡 · environmental effects (wind, gravity variation, temperature/humidity, canvas shake) ⬜ (listed as *suggested*; tank Shaker covers "moving surface" idea for the tank only).

## §8 Project stages
1 Study/analysis ✅ (reference study submitted) · 2 Model design ✅ (params + UI) · 3 Physics sim ✅ · 4 Painting + multi-colour ✅ · 5 Testing & improvement 🟡 = roadmap **M8** (compare with real artworks, tune values, measure performance — the ms/FPS instrumentation already serves the "measure" part).

## §9 Extra ideas (optional)
Multiple buckets ⬜ · game/educational app ⬜ · video / HQ export 🟡 (PNG ✅, video ⬜) · AI painting generation ⬜ (marked Extra).

## Evaluation criteria (آلية التقييم) — mapping
1. Reference study & proposed approach ✅ · 2. Rendering quality/speed & realism 🟡 (M8 tuning) · 3. Correct physics per object properties ✅ (SPH + analytic boundaries, verified) · 4. **Controllability of everything** — colours ✅, canvas type ⬜, canvas size ✅, bucket path 🟡, gravity ✅, humidity ⬜ → the ⬜ items here are the highest-value gaps · 5. Execution quality/team ✅ · 6. Proposing mechanisms that speed up execution ✅ (uniform-grid O(n) + half-stencil optimisation + live proof).

## Constraint (ملاحظات §4)
No Unity physics tools (RigidBody/Colliders/…) and no ready-made libraries — **fully honoured**: all motion, fluid, collision and painting logic is hand-written C# (see SPH & analytic-boundary code).

---

## Gap plan (agreed order)
1. ~~**M6 — outputs 5/6/7**~~ ✅ done 2026-07-06.
2. **Required-inputs quick wins**: canvas tilt · air resistance · humidity (splat spread) · swing count · canvas surface type presets.
3. **Justify-or-add**: bucket weight/radius, rope elasticity, suspension point, swing-plane direction.
4. **M8**: realism tuning vs real artworks + performance pass + code cleanup.
