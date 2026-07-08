using System.Collections.Generic;
using UnityEngine;

// A procedurally generated bail: the curved metal handle of a paint pail. Built entirely in code
// as a thin tube swept along a half-ellipse over the bucket mouth — no imported model, no physics.
// It lives as a CHILD of the bucket, so it tilts with it while swinging, and it drives the bucket's
// rope attach point to the bail's APEX so the rope hangs from the handle like a real pail.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BucketHandle : MonoBehaviour
{
    [Header("Bail shape (local units)")]
    public float rise = 0.55f;                  // how high the bail arcs above the rim
    public float thickness = 0.03f;             // wire radius
    [Range(6, 48)] public int arcSegments = 24; // samples along the arc
    [Range(4, 16)] public int tubeSegments = 8; // ring resolution around the wire

    ProceduralBucket pb;   // cached parent bucket
    Bucket bucket;         // cached pendulum (owns the rope attach point)

    void OnEnable() { CacheRefs(); Rebuild(); }

    void CacheRefs()
    {
        pb = GetComponentInParent<ProceduralBucket>();
        bucket = pb != null ? pb.GetComponent<Bucket>() : GetComponentInParent<Bucket>();
    }

    void OnValidate()
    {
        // Mesh-only rebuild is safe from OnValidate (we never create GameObjects here).
        if (isActiveAndEnabled && gameObject.scene.IsValid()) Rebuild();
    }

    public void Rebuild()
    {
        CacheRefs();
        float R  = pb != null ? pb.topRadius : 0.5f;      // bail ends sit at the rim edges (±R)
        float hh = (pb != null ? pb.height : 1f) * 0.5f;  // rim height in bucket-local space

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale    = Vector3.one;

        var mf = GetComponent<MeshFilter>();
        var mesh = mf.sharedMesh;
        if (mesh == null || mesh.name != "BucketHandle")
        {
            mesh = new Mesh { name = "BucketHandle" };
            mf.sharedMesh = mesh;
        }
        BuildBail(mesh, R, hh);

        // The rope attaches to the bail's apex (the top of the arc), so it hangs from the handle.
        if (bucket != null)
        {
            Vector3 apexLocal = new Vector3(0f, hh + rise, 0f);
            bucket.ropeAttachLocal = apexLocal;                 // fallback when no apex transform is wired
            var apex = bucket.transform.Find("RopeApex");        // the real point the rope pins to
            if (apex != null) apex.localPosition = apexLocal;
        }
    }

    // Half-ellipse over the mouth: ends at the rim edges (±R, hh), apex at (0, hh + rise).
    Vector3 PathAt(float s, float R, float hh) =>
        new Vector3(-R * Mathf.Cos(s), hh + rise * Mathf.Sin(s), 0f);

    void BuildBail(Mesh mesh, float R, float hh)
    {
        int N = Mathf.Max(6, arcSegments);
        int M = Mathf.Max(4, tubeSegments);
        var verts = new List<Vector3>((N + 1) * M);
        var tris  = new List<int>(N * M * 6);

        for (int i = 0; i <= N; i++)
        {
            float s = Mathf.PI * i / N;
            Vector3 c = PathAt(s, R, hh);

            // Tangent by central difference; the path lies in the X-Y plane.
            const float ds = 0.001f;
            Vector3 t = PathAt(Mathf.Min(Mathf.PI, s + ds), R, hh) - PathAt(Mathf.Max(0f, s - ds), R, hh);
            if (t.sqrMagnitude < 1e-10f) t = Vector3.right;
            t.Normalize();

            // A round frame perpendicular to the tangent: b1 in the X-Y plane, b2 along Z.
            Vector3 b1 = Vector3.Cross(t, Vector3.forward);
            if (b1.sqrMagnitude < 1e-10f) b1 = Vector3.up;
            b1.Normalize();
            Vector3 b2 = Vector3.forward;

            for (int j = 0; j < M; j++)
            {
                float a = 2f * Mathf.PI * j / M;
                verts.Add(c + (Mathf.Cos(a) * b1 + Mathf.Sin(a) * b2) * thickness);
            }
        }

        for (int i = 0; i < N; i++)
            for (int j = 0; j < M; j++)
            {
                int nj = (j + 1) % M;
                int a = i * M + j, b = i * M + nj, cc = (i + 1) * M + j, d = (i + 1) * M + nj;
                tris.Add(a); tris.Add(cc); tris.Add(d);
                tris.Add(a); tris.Add(d);  tris.Add(b);
            }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
