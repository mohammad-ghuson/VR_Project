using UnityEngine;

// M4 - Painting canvas. A flat quad with a writable texture. Paint particles that reach it
// (M4.2) stamp colored splats into the texture; they accumulate into the artwork.
// No colliders: the canvas is an analytic plane (center + normal + in-plane axes), and the
// hit test is plain math. Splats are batched into a pixel buffer and uploaded once per frame.
[ExecuteAlways]
public class PaintCanvas : MonoBehaviour
{
    [Header("Canvas")]
    public int textureResolution = 1024;
    public Color surfaceColor = new Color(0.95f, 0.95f, 0.92f, 1f); // canvas/paper base
    [Range(0f, 1f)] public float edgeSoftness = 0.5f;               // M4.3: soft splat edges

    Texture2D tex;
    Color32[] pixels;
    bool dirty;
    MeshRenderer rend;
    Vector2Int lastPx;   // M4.3: previous stamp pixel, to connect into continuous strokes
    bool hasLast;

    // --- Analytic plane (world space), refreshed each frame so it follows the transform ---
    public Vector3 PlaneCenter { get; private set; }
    public Vector3 PlaneNormal { get; private set; }   // the face paint lands on (local +Y)
    public Vector3 PlaneAxisU { get; private set; }    // right * half-width
    public Vector3 PlaneAxisV { get; private set; }    // forward * half-depth

    void OnEnable()
    {
        EnsureTexture();
        UpdatePlane();
    }

    void Update()
    {
        UpdatePlane();
        if (dirty && tex != null)
        {
            tex.SetPixels32(pixels);
            tex.Apply(false);
            dirty = false;
        }
    }

    void EnsureTexture()
    {
        rend = GetComponent<MeshRenderer>();
        if (tex == null)
        {
            int res = Mathf.Max(64, textureResolution);
            tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            pixels = new Color32[res * res];
            Clear();
        }
        if (rend != null && rend.sharedMaterial != null)
            rend.sharedMaterial.mainTexture = tex;
    }

    // Reset the whole canvas to the base surface color.
    public void Clear()
    {
        if (tex == null) return;
        Color32 c = surfaceColor;
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
        tex.SetPixels32(pixels);
        tex.Apply(false);
        dirty = false;
        hasLast = false;
    }

    // M5.5 - Save the current artwork as a PNG file and return its full path.
    public string SavePng()
    {
        if (tex == null) return null;
        byte[] bytes = tex.EncodeToPNG();
        // Save next to the project (Assets/..), so it's easy to find: <Project>/SavedPaintings.
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "SavedPaintings"));
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, "painting_" +
            System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log("[PaintCanvas] Saved painting to: " + path);
        return path;
    }

    // Try to paint at a world point. Returns true (and stamps) if the point reached the
    // canvas plane (within hitDist) AND lies within the canvas bounds. Used by SphFluid.
    public bool TryPaint(Vector3 worldPoint, float hitDist, Color color, float splatWorldRadius)
    {
        if (tex == null) return false;

        float d = Vector3.Dot(worldPoint - PlaneCenter, PlaneNormal);
        if (d > hitDist) return false; // still above the canvas

        float halfW = PlaneAxisU.magnitude, halfD = PlaneAxisV.magnitude;
        if (halfW < 1e-5f || halfD < 1e-5f) return false;

        Vector3 rel = worldPoint - PlaneCenter;
        float du = Vector3.Dot(rel, PlaneAxisU / halfW);
        float dv = Vector3.Dot(rel, PlaneAxisV / halfD);
        float uvX = du / (2f * halfW) + 0.5f;
        float uvY = dv / (2f * halfD) + 0.5f;
        if (uvX < 0f || uvX > 1f || uvY < 0f || uvY > 1f) return false; // off-canvas

        int cx = Mathf.RoundToInt(uvX * (tex.width - 1));
        int cy = Mathf.RoundToInt(uvY * (tex.height - 1));
        int rPx = Mathf.Max(1, Mathf.RoundToInt(splatWorldRadius / (2f * halfW) * tex.width));
        Color32 c32 = color;

        // Connect nearby hits into a continuous stroke; far jumps just dot.
        float gap = Vector2.Distance(new Vector2(cx, cy), lastPx);
        if (hasLast && gap < tex.width * 0.06f)
            StampLine(lastPx.x, lastPx.y, cx, cy, rPx, c32);
        else
            StampCircle(cx, cy, rPx, c32);

        lastPx = new Vector2Int(cx, cy);
        hasLast = true;
        return true;
    }

    // Soft-edged filled circle: full color at the center, fading out toward the rim,
    // blended over what's already there (so paint layers build up like real paint).
    void StampCircle(int cx, int cy, int r, Color32 col)
    {
        int w = tex.width, h = tex.height;
        int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(w - 1, cx + r);
        int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(h - 1, cy + r);
        float soft = Mathf.Clamp01(edgeSoftness);
        float inner = 1f - soft;
        for (int y = y0; y <= y1; y++)
        {
            int row = y * w;
            int dy = y - cy;
            for (int x = x0; x <= x1; x++)
            {
                int dx = x - cx;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > r) continue;
                float t = r > 0 ? dist / r : 0f;          // 0 center .. 1 edge
                float a = t <= inner ? 1f : 1f - (t - inner) / Mathf.Max(soft, 1e-4f);
                if (a <= 0f) continue;
                int idx = row + x;
                pixels[idx] = Color32.Lerp(pixels[idx], col, a);
            }
        }
        dirty = true;
    }

    // Stamp a thick soft line by laying overlapping circles along the segment.
    void StampLine(int x0, int y0, int x1, int y1, int r, Color32 col)
    {
        float len = Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1));
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(1f, r * 0.5f)));
        for (int s = 0; s <= steps; s++)
        {
            float f = (float)s / steps;
            StampCircle(Mathf.RoundToInt(Mathf.Lerp(x0, x1, f)),
                        Mathf.RoundToInt(Mathf.Lerp(y0, y1, f)), r, col);
        }
    }

    void UpdatePlane()
    {
        PlaneCenter = transform.position;
        PlaneNormal = transform.up;                       // quad faces up
        PlaneAxisU = transform.right * (transform.lossyScale.x * 0.5f);
        PlaneAxisV = transform.forward * (transform.lossyScale.z * 0.5f);
    }
}
