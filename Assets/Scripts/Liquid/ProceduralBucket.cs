using System.Collections.Generic;
using UnityEngine;

// A procedurally generated open bucket: a regular (optionally tapered) cylinder wall + a bottom
// disk, OPEN at the top. Built entirely in code — no imported model, no physics engine. It replaces
// the irregular downloaded model so that:
//   * the shape is perfectly regular,
//   * the SPH fluid container matches the visible walls exactly (no fluid outside the walls),
//   * there is a clean, known top point to hang the rope from.
// The wall is rendered double-sided and semi-transparent (set by the builder menu) so the liquid
// inside stays visible.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralBucket : MonoBehaviour
{
    [Header("Shape (local units)")]
    public float topRadius = 0.7f;
    public float bottomRadius = 0.53f;
    public float height = 1.5f;
    [Range(8, 64)] public int segments = 32;

    [Header("Fluid match")]
    public SphFluid fluid;            // its analytic container is matched to this bucket
    public float wallInset = 0.05f;   // keep the fluid boundary this far inside the wall

    [Header("Fluid amount")]
    public bool autoFillAmount = true;            // pick a particle count that fills the bucket
    [Range(0.3f, 1f)] public float fillFraction = 0.9f; // how full the bucket starts

    // One-click: apply the studied "paint bucket" proportions and rebuild. Right-click the
    // ProceduralBucket component header in the Inspector -> "Apply Recommended Shape".
    [ContextMenu("Apply Recommended Shape")]
    public void ApplyRecommendedShape()
    {
        topRadius = 0.7f;
        bottomRadius = 0.53f;
        height = 1.5f;
        segments = 32;
        fillFraction = 0.9f;
        Rebuild();
    }

    void OnEnable() { Rebuild(); }

    void OnValidate()
    {
        // Rebuild live while tuning in the Inspector (guard against prefab/asset validation).
        if (isActiveAndEnabled && gameObject.scene.IsValid()) Rebuild();
    }

    public void Rebuild()
    {
        var mf = GetComponent<MeshFilter>();
        var mesh = mf.sharedMesh;
        if (mesh == null || mesh.name != "ProceduralBucket")
        {
            mesh = new Mesh { name = "ProceduralBucket" };
            mf.sharedMesh = mesh;
        }
        BuildInto(mesh);
        MatchFluid();
    }

    void BuildInto(Mesh mesh)
    {
        int s = Mathf.Max(8, segments);
        var verts = new List<Vector3>(s * 2 + 1);
        var tris = new List<int>(s * 12);
        float hh = height * 0.5f;

        for (int i = 0; i < s; i++) { float a = 2f * Mathf.PI * i / s; verts.Add(new Vector3(Mathf.Cos(a) * topRadius, hh, Mathf.Sin(a) * topRadius)); }
        for (int i = 0; i < s; i++) { float a = 2f * Mathf.PI * i / s; verts.Add(new Vector3(Mathf.Cos(a) * bottomRadius, -hh, Mathf.Sin(a) * bottomRadius)); }

        // Side wall (quads between the top and bottom rings).
        for (int i = 0; i < s; i++)
        {
            int ni = (i + 1) % s;
            int t0 = i, t1 = ni, b0 = s + i, b1 = s + ni;
            tris.Add(t0); tris.Add(b0); tris.Add(b1);
            tris.Add(t0); tris.Add(b1); tris.Add(t1);
        }

        // Bottom cap (a triangle fan).
        int c = verts.Count; verts.Add(new Vector3(0f, -hh, 0f));
        for (int i = 0; i < s; i++)
        {
            int ni = (i + 1) % s;
            tris.Add(c); tris.Add(s + ni); tris.Add(s + i);
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // Drive the SPH analytic container from the bucket's real dimensions, so the fluid always fits;
    // pick a particle count that fills the bucket; and tell the rope where the bucket top is.
    void MatchFluid()
    {
        // Rope attaches to the bucket's top rim centre (its real geometry).
        var bucket = GetComponent<Bucket>();
        if (bucket != null) bucket.ropeAttachLocal = new Vector3(0f, height * 0.5f, 0f);

        if (fluid == null) return;
        Vector3 sc = transform.lossyScale;
        fluid.manualFit = true;
        fluid.fitRadius = (Mathf.Min(topRadius, bottomRadius) - wallInset) * Mathf.Abs(sc.x);
        fluid.fitHalfHeight = (height * 0.5f - wallInset) * Mathf.Abs(sc.y);
        fluid.fitCenterLocal = Vector3.zero; // mesh is centred on the transform origin

        // Fill the bucket to ~fillFraction: the spawn packs perRow^2 particles per layer and stacks
        // layers; choose the count so the column height matches the target fill.
        if (autoFillAmount && fluid.particleRadius > 0f && fluid.fitRadius > 0f)
        {
            float sp = fluid.particleRadius * 2.1f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((fluid.fitRadius * 1.4f) / sp));
            int layers = Mathf.Max(1, Mathf.FloorToInt(fillFraction * (2f * fluid.fitHalfHeight) / sp));
            fluid.particleCount = perRow * perRow * layers;
        }
    }
}
