# Deep Dive — Canvas, Painting, Complexity, and the Tools Menu

**Project:** Swinging Paint Bucket (VR) · Unity 6000.4.10f1 · URP 17.4
**Audience:** the author (to understand and present) and the supervisor.
This explains, with real code excerpts, four things:
1. how the canvas was added and what kind of object it is,
2. how painting on it works,
3. how we lowered the neighbour-search cost,
4. how the `Tools` menu commands work.

All logic is our own C# math — no physics engine (no Rigidbody / Collider / Joint), no fluid
packages.

---

## 1. The canvas — what it is and how we added it

### What kind of object is it?
The canvas is **not** a Unity UI (uGUI) Canvas. It is an ordinary **3-D quad mesh** with a
`MeshRenderer`, carrying a **writable `Texture2D`** that we paint into pixel-by-pixel. We describe
its surface **analytically** (a plane) so paint hits are tested with plain math instead of a
collider.

### How it is created
An editor command builds the object: a hand-made quad mesh, a material, and the `PaintCanvas`
component. From `LiquidSurfaceTools.cs`:

```csharp
[MenuItem("Tools/Liquid/Canvas - M4.1 Create Canvas")]
static void CreateCanvas()
{
    var go = new GameObject("PaintCanvas");
    go.AddComponent<MeshFilter>().sharedMesh = CreateOrLoadQuadMesh();
    go.AddComponent<MeshRenderer>().sharedMaterial = CreateCanvasMaterial();
    go.AddComponent<PaintCanvas>();

    go.transform.position   = new Vector3(0f, 0.05f, 0f);   // just above the ground
    go.transform.localScale = new Vector3(6f, 1f, 6f);      // 6x6 world units
}
```

The quad is built by us (so there is no built-in primitive and no collider). It lies flat in the
local XZ plane, normal `+Y`, UVs `0..1`:

```csharp
// Flat quad in local XZ (normal +Y, UV 0..1) — the painting surface.
m.vertices  = new[] {
    new Vector3(-0.5f,0f,-0.5f), new Vector3(0.5f,0f,-0.5f),
    new Vector3( 0.5f,0f, 0.5f), new Vector3(-0.5f,0f,0.5f) };
m.normals   = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
m.uv        = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
```

### The writable texture
`PaintCanvas` allocates an RGBA32 `Texture2D`, fills it with a paper colour, and assigns it to the
material. All stamps are written into a `Color32[]` buffer and uploaded **once per frame** (batched
`SetPixels32` + `Apply`) for performance:

```csharp
tex = new Texture2D(res, res, TextureFormat.RGBA32, false);   // default 1024x1024
pixels = new Color32[res * res];
...
void Update() {
    UpdatePlane();
    if (dirty && tex != null) { tex.SetPixels32(pixels); tex.Apply(false); dirty = false; }
}
```

### The analytic plane (no collider)
Each frame we refresh the plane from the transform: a centre, a normal, and two in-plane axes
scaled to half-width/half-depth. This is what lets us test paint hits with a dot product:

```csharp
void UpdatePlane() {
    PlaneCenter = transform.position;
    PlaneNormal = transform.up;                                   // face paint lands on
    PlaneAxisU  = transform.right   * (transform.lossyScale.x * 0.5f);
    PlaneAxisV  = transform.forward * (transform.lossyScale.z * 0.5f);
}
```

---

## 2. How painting works

### Step 1 — a particle reaches the canvas
In the SPH solver, a particle that has drained out of the bucket ("escaped") is tested against the
canvas every step. If `TryPaint` succeeds, the particle is consumed; otherwise it keeps falling and
is removed below a despawn plane (`SphFluid.Step`):

```csharp
else // escaped (free-falling) paint
{
    if (paintCanvas != null &&
        paintCanvas.TryPaint(positions[i], particleRadius, paintColor, splatRadius))
        dead[i] = true;
    else if (positions[i].y < despawnBelowY)
        dead[i] = true;
}
```

### Step 2 — plane hit-test → texture UV
`TryPaint` projects the world point onto the plane. If it is within `hitDist` of the plane **and**
inside the canvas bounds, it converts the position to a UV (0..1) and then to pixel coordinates:

```csharp
public bool TryPaint(Vector3 worldPoint, float hitDist, Color color, float splatWorldRadius)
{
    float d = Vector3.Dot(worldPoint - PlaneCenter, PlaneNormal);
    if (d > hitDist) return false;                       // still above the canvas

    Vector3 rel = worldPoint - PlaneCenter;
    float uvX = Vector3.Dot(rel, PlaneAxisU / halfW) / (2f*halfW) + 0.5f;
    float uvY = Vector3.Dot(rel, PlaneAxisV / halfD) / (2f*halfD) + 0.5f;
    if (uvX < 0f || uvX > 1f || uvY < 0f || uvY > 1f) return false;   // off-canvas

    int cx = Mathf.RoundToInt(uvX * (tex.width  - 1));
    int cy = Mathf.RoundToInt(uvY * (tex.height - 1));
    ...
}
```

### Step 3 — stamp a soft mark, connect strokes
If the new hit is close to the previous one we draw a **line** between them (a continuous brush
stroke); otherwise we drop a **dot**:

```csharp
float gap = Vector2.Distance(new Vector2(cx, cy), lastPx);
if (hasLast && gap < tex.width * 0.06f) StampLine(lastPx.x, lastPx.y, cx, cy, rPx, c32);
else                                    StampCircle(cx, cy, rPx, c32);
```

Each splat is full colour at the centre and fades to transparent at the rim, **blended** over what
is already there so paint layers build up like real paint:

```csharp
float t = dist / r;                                  // 0 centre .. 1 edge
float a = t <= inner ? 1f : 1f - (t - inner) / soft; // soft falloff
pixels[idx] = Color32.Lerp(pixels[idx], col, a);     // blend onto existing paint
```

### Saving the artwork
`SavePng()` encodes the texture and writes it next to the project so it is easy to find:

```csharp
byte[] bytes = tex.EncodeToPNG();
string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SavedPaintings"));
Directory.CreateDirectory(dir);
File.WriteAllBytes(Path.Combine(dir, "painting_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"), bytes);
```

---

## 3. How we lowered the neighbour-search cost

### The starting point: O(n) via a uniform grid
Each particle only interacts within the smoothing radius `h`. We bin particles into a grid whose
cell size is `h`, so a particle's neighbours can only be in its own cell + the 26 around it (27
cells). This makes the search **O(n)** — the "27" is a *constant*, not part of the order:

```csharp
Vector3Int CellOf(Vector3 p) {
    float inv = 1f / Mathf.Max(smoothingRadius, 1e-4f);
    return new Vector3Int(Mathf.FloorToInt(p.x*inv), Mathf.FloorToInt(p.y*inv), Mathf.FloorToInt(p.z*inv));
}
```

### The optimisation (S1): one pass + a half stencil
Previously the neighbours were searched **twice per step** (once for density, once for forces),
each scanning 27 cells. Now we build each particle's list **once** using a **half stencil**: 13
"forward" cells plus same-cell pairs. Every unordered pair is found once and pushed onto **both**
lists (Newton's-3rd-law style), so the produced lists are identical but the search does far less
work (~`2×27n` → ~`13n`). The 13 offsets:

```csharp
// One of each opposite pair of the 26 neighbours -> scanning them + home visits every pair once.
if (dz > 0 || (dz == 0 && dy > 0) || (dz == 0 && dy == 0 && dx > 0))
    list.Add(new Vector3Int(dx, dy, dz));
```

```csharp
void BuildNeighbors() {
    for (int i = 0; i < neighbors.Length; i++) neighbors[i].Clear();
    float h2 = smoothingRadius * smoothingRadius;
    foreach (var kv in grid) {
        var home = kv.Value;
        for (int a = 0; a < home.Count; a++)               // same-cell pairs (b > a)
            for (int b = a + 1; b < home.Count; b++)
                if ((positions[home[b]] - positions[home[a]]).sqrMagnitude <= h2)
                    { neighbors[home[a]].Add(home[b]); neighbors[home[b]].Add(home[a]); }

        for (int f = 0; f < ForwardCells.Length; f++)      // 13 forward cells
            if (grid.TryGetValue(kv.Key + ForwardCells[f], out var other))
                for (int a = 0; a < home.Count; a++)
                    for (int b = 0; b < other.Count; b++)
                        if ((positions[other[b]] - positions[home[a]]).sqrMagnitude <= h2)
                            { neighbors[home[a]].Add(other[b]); neighbors[other[b]].Add(home[a]); }
    }
}
```

The density and force loops then just iterate the cached list, e.g.:

```csharp
var nb = neighbors[i];               // S1: reuse the cached neighbour list
for (int k = 0; k < nb.Count; k++) { int j = nb[k]; /* pressure + viscosity */ }
```

**This does not change the order** — it is still O(n) (which is optimal, since every particle must
be touched). It shrinks the *constant* from ~27n to ~13n.

### Proving it is correct
A self-check compares the cached lists with an independent brute-force search for every particle;
it logs `PASS` only if all sets match exactly (they did):

```csharp
GetNeighbors(i, neighborScratch);            // independent search
// ... compare as a set to neighbors[i] ...
Debug.Log(mismatches == 0 ? "[SPH][Verify] PASS ..." : "[SPH][Verify] FAIL ...");
```

### Demonstrating the complexity live
A switch runs the naive O(n²) search instead — identical result, far slower — so we can measure the
gap on screen:

```csharp
void BuildNeighborsBruteForce() {                // O(n^2): every pair tested
    for (int i = 0; i < positions.Length; i++)
        for (int j = i + 1; j < positions.Length; j++)
            if ((positions[j] - positions[i]).sqrMagnitude <= h2)
                { neighbors[i].Add(j); neighbors[j].Add(i); }
}
```

We time the search with a `Stopwatch` (FPS is a poor metric under VSync). Measured at 3000
particles: **Grid ≈ 14.4 ms** vs **BruteForce ≈ 65.2 ms** (~4.5×). Doubling `n` grows the grid
time ~×2 (linear) but the brute time ~×4 (quadratic) — O(n) vs O(n²), measured.

> Why not O(log n)? You cannot beat O(n): every particle must be updated. Trees give O(n·log n)
> total — worse — and the SPH kernel has compact support (zero beyond h), so there is no far field
> for tree/FMM methods to approximate. The uniform grid is optimal; only the constant (or parallel
> hardware) can improve.

---

## 4. How the `Tools` menu commands work

The commands come from **editor scripting**. A `static` method tagged with `[MenuItem("path")]`
(from the `UnityEditor` namespace) appears in the menu at that path and runs when clicked. The file
lives in an `Editor/` folder, so Unity compiles it into an **editor-only assembly excluded from the
build**.

```csharp
[MenuItem("Tools/Liquid/UI - M5.1 Create Control Panel")]
static void CreateControlPanel()
{
    var canvasGO = new GameObject("ControlPanelUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
    canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
    ...
    var ctrl = canvasGO.AddComponent<UIControlPanel>();          // runtime script
    ctrl.bucket = Object.FindFirstObjectByType<Bucket>();        // auto-wire references
    EditorSceneManager.MarkSceneDirty(canvasGO.scene);
}
```

The methods build GameObjects and components programmatically. For example, each slider row is
constructed in code (label + value + a fully wired `Slider`) by reusable helpers:

```csharp
static Slider BuildControlRow(RectTransform panel, string label, float min, float max,
                              float value, int index, out Text valueText) {
    ... NewLabel(...);                        // name on the left
    valueText = NewLabel(...);                // live value on the right
    return NewSlider(..., min, max, value);   // the slider underneath
}
```

```csharp
static Slider NewSlider(...) {               // build background + fill + handle by hand
    var s = go.GetComponent<Slider>();
    s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform;
    s.minValue = min; s.maxValue = max; s.value = value;
    return s;
}
```

**Why do it this way?**
- **Repeatable & documented:** the code builds the exact same UI every time and is tracked in Git,
  unlike manual drag-and-drop.
- **Separation of concerns:** the menus are *build tooling* (editor-time). The actual behaviour is
  in *runtime* MonoBehaviours (`SphFluid`, `UIControlPanel`, `PaintCanvas`).
- **Persisted:** the objects the menu creates are saved in the scene and run at play time without
  the menu — the menu is a one-time builder, not a runtime dependency.

Editor-only calls used here: `GameObject`/`AddComponent` creation, `Undo.RegisterCreatedObjectUndo`,
`EditorSceneManager.MarkSceneDirty`, `AssetDatabase.CreateAsset` (for saved meshes/materials), and
type-name lookup to attach the correct input module for the new Input System.

---

## 5. One-paragraph summary (for the interview)
The canvas is a plain textured quad we built ourselves, described analytically as a plane; falling
SPH paint is tested against that plane and, on a hit, stamped as a soft, stroke-connected splat into
a writable texture that we can export as PNG. The fluid's neighbour search is O(n) via a uniform
grid (optimal); we cut the constant from ~27n to ~13n by computing neighbours once per step with a
half stencil, and proved equivalence with a self-check (PASS). A live toggle measures our O(n) grid
against a naive O(n²) baseline (≈14 ms vs ≈65 ms at 3000). All scene/UI construction is automated
through editor `[MenuItem]` tools that build and wire GameObjects programmatically, while the runtime
logic stays in ordinary MonoBehaviours.
