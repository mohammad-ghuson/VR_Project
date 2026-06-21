using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Phase 1 - Step 0 + Step 1 ONLY.
// Step 0: inspect the bucket and report its real bounds (measured inside Unity).
// Step 1: create a flat "LiquidSurface" disc sized/positioned inside the bucket.
// No custom shader is authored here (see plan). A simple URP/Lit colored material
// is created only so the disc is visible for the initial visual test.
public static class LiquidSurfaceTools
{
    // --- Tunables: edit then re-run, or tweak the created object in the Inspector ---
    const float InnerRadiusFactor = 0.85f; // liquid radius = outer radius * this
    const float FillLevel = 0.5f;          // 0 = bucket bottom, 1 = bucket top
    const int   DiscSegments = 64;         // resolution of the hand-built disc mesh

    [MenuItem("Tools/Liquid/Step 0 - Inspect Bucket")]
    static void InspectBucket()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogError("[Liquid] Select the Bucket GameObject in the Hierarchy first."); return; }
        if (!TryGetBounds(go, out Bounds b)) { Debug.LogError("[Liquid] No Renderer found under '" + go.name + "'. Is this the bucket?"); return; }

        Vector3 c = b.center, s = b.size;
        float radius = Mathf.Min(s.x, s.z) * 0.5f;
        Debug.Log(
            "[Liquid][Step0] Bucket: " + go.name +
            "\n World center : " + c.ToString("F3") +
            "\n World size   : " + s.ToString("F3") +
            "\n Bottom Y     : " + (c.y - s.y * 0.5f).ToString("F3") +
            "\n Top Y        : " + (c.y + s.y * 0.5f).ToString("F3") +
            "\n Outer radius (min XZ half) : " + radius.ToString("F3") +
            "\n Suggested inner radius (x" + InnerRadiusFactor + ") : " + (radius * InnerRadiusFactor).ToString("F3") +
            "\n Pivot (transform.position) : " + go.transform.position.ToString("F3") +
            "\n LossyScale   : " + go.transform.lossyScale.ToString("F3") +
            "\n NOTE: inner radius is approximated from the OUTER AABB (the FBX is not readable, " +
            "so true wall geometry can't be measured here). Confirm visually that the disc sits inside the walls.",
            go);
    }

    [MenuItem("Tools/Liquid/Step 1 - Create Liquid Surface")]
    static void CreateLiquidSurface()
    {
        var bucket = Selection.activeGameObject;
        if (bucket == null) { Debug.LogError("[Liquid] Select the Bucket GameObject in the Hierarchy first."); return; }

        var existing = bucket.transform.Find("LiquidSurface");
        if (existing != null)
        {
            Debug.LogWarning("[Liquid] 'LiquidSurface' already exists. Delete it before recreating.", existing.gameObject);
            Selection.activeObject = existing.gameObject;
            return;
        }

        if (!TryGetBounds(bucket, out Bounds b)) { Debug.LogError("[Liquid] No Renderer found under '" + bucket.name + "'."); return; }

        Vector3 c = b.center, s = b.size;
        float radiusWorld = Mathf.Min(s.x, s.z) * 0.5f * InnerRadiusFactor;

        // Build the object ourselves: no Unity primitive => no built-in mesh and no collider.
        var disc = new GameObject("LiquidSurface");
        Undo.RegisterCreatedObjectUndo(disc, "Create Liquid Surface");
        var mf = disc.AddComponent<MeshFilter>();
        var mr = disc.AddComponent<MeshRenderer>();
        mf.sharedMesh = CreateOrLoadDiscMesh();
        mr.sharedMaterial = CreatePaintMaterial();

        Undo.SetTransformParent(disc.transform, bucket.transform, "Parent Liquid Surface");

        // World placement at the chosen fill level.
        float bottom = c.y - s.y * 0.5f;
        disc.transform.position = new Vector3(c.x, bottom + FillLevel * s.y, c.z);
        disc.transform.rotation = Quaternion.identity;

        // Our disc mesh has radius 0.5 (diameter 1) and is flat, so world diameter = scale.x.
        // Convert desired WORLD size into local scale to cancel the parent's scale.
        Vector3 ws = new Vector3(radiusWorld * 2f, 1f, radiusWorld * 2f);
        Vector3 ps = bucket.transform.lossyScale;
        disc.transform.localScale = new Vector3(
            ws.x / Mathf.Max(Mathf.Abs(ps.x), 1e-5f),
            ws.y / Mathf.Max(Mathf.Abs(ps.y), 1e-5f),
            ws.z / Mathf.Max(Mathf.Abs(ps.z), 1e-5f));

        Selection.activeObject = disc;
        EditorSceneManager.MarkSceneDirty(disc.scene);
        Debug.Log("[Liquid][Step1] Created 'LiquidSurface' under '" + bucket.name +
                  "' at fill " + FillLevel + ", world radius " + radiusWorld.ToString("F3") +
                  ". Fine-tune by editing its transform if needed.", disc);
    }

    [MenuItem("Tools/Liquid/Step 2 - Add Liquid Controller")]
    static void AddLiquidController()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) { Debug.LogError("[Liquid] Select the Bucket or the LiquidSurface first."); return; }

        Transform surface = sel.name == "LiquidSurface" ? sel.transform : sel.transform.Find("LiquidSurface");
        if (surface == null) { Debug.LogError("[Liquid] No 'LiquidSurface' found under '" + sel.name + "'. Run Step 1 first."); return; }

        if (surface.GetComponent<LiquidController>() != null)
        {
            Debug.LogWarning("[Liquid] LiquidController is already attached.", surface.gameObject);
            Selection.activeObject = surface.gameObject;
            return;
        }

        Undo.AddComponent<LiquidController>(surface.gameObject);
        Selection.activeObject = surface.gameObject;
        EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
        Debug.Log("[Liquid][Step2] Added LiquidController to 'LiquidSurface'. " +
                  "Rotate the Bucket in the Scene view and confirm the surface stays level.", surface.gameObject);
    }

    [MenuItem("Tools/Liquid/Step 3 - Apply Liquid Shader")]
    static void ApplyLiquidShader()
    {
        if (Shader.Find("Custom/LiquidPaint") == null)
        {
            Debug.LogError("[Liquid] Shader 'Custom/LiquidPaint' not found. " +
                           "Make sure LiquidPaint.shader compiled (check the Console for shader errors).");
            return;
        }

        var mat = CreatePaintMaterial(); // creates the material or swaps its shader to ours

        // Make sure the existing LiquidSurface uses this material.
        var sel = Selection.activeGameObject;
        Transform surface = null;
        if (sel != null) surface = sel.name == "LiquidSurface" ? sel.transform : sel.transform.Find("LiquidSurface");
        if (surface != null)
        {
            var r = surface.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[Liquid][Step3] Applied 'Custom/LiquidPaint' to the LiquidPaint material. " +
                  "Tune Base/Rim/Specular on the material in the Inspector.", mat);
    }

    [MenuItem("Tools/Liquid/Step 4 - Add Bucket Tilt")]
    static void AddBucketTilt()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) { Debug.LogError("[Liquid] Select the Bucket GameObject first."); return; }

        // Prefer the object that actually carries the Bucket motion script.
        var bucketComp = sel.GetComponentInChildren<Bucket>();
        GameObject target = bucketComp != null ? bucketComp.gameObject : sel;

        if (target.GetComponent<BucketTilt>() != null)
        {
            Debug.LogWarning("[Liquid] BucketTilt is already attached.", target);
            Selection.activeObject = target;
            return;
        }

        Undo.AddComponent<BucketTilt>(target);
        Selection.activeObject = target;
        EditorSceneManager.MarkSceneDirty(target.scene);
        Debug.Log("[Liquid][Step4] Added BucketTilt to '" + target.name +
                  "'. Press Play: the bucket should swing & tilt while the liquid stays level.", target);
    }

    [MenuItem("Tools/Liquid/SPH - Step A - Create SPH Fluid")]
    static void CreateSphFluid()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) { Debug.LogError("[SPH] Select the Bucket GameObject first."); return; }

        var bucketComp = sel.GetComponentInChildren<Bucket>();
        Transform bucketT = bucketComp != null ? bucketComp.transform : sel.transform;

        var go = new GameObject("SPHFluid");
        Undo.RegisterCreatedObjectUndo(go, "Create SPH Fluid");
        var sph = go.AddComponent<SphFluid>();
        sph.bucket = bucketT;
        sph.particleMaterial = CreatePaintMaterial();

        Selection.activeObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[SPH][StepA] Created 'SPHFluid' wired to '" + bucketT.name +
                  "'. TIP: temporarily DISABLE the Bucket component (uncheck it) for this test " +
                  "so the bucket stays still while particles fall into it.", go);
    }

    static bool TryGetBounds(GameObject go, out Bounds b)
    {
        b = new Bounds(go.transform.position, Vector3.zero);
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return false;
        b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return true;
    }

    // Hand-built flat disc (triangle fan). Radius 0.5, lying in the XZ plane.
    // Double-sided so it stays visible from any viewing angle. Saved as an asset
    // so the reference survives scene/domain reloads.
    static Mesh CreateOrLoadDiscMesh()
    {
        const string path = "Assets/Meshes/LiquidDisc.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        int seg = Mathf.Max(8, DiscSegments);
        var verts = new Vector3[seg + 2];
        var normals = new Vector3[seg + 2];
        var uvs = new Vector2[seg + 2];

        verts[0] = Vector3.zero;            // center vertex
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i <= seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            verts[i + 1] = new Vector3(cx * 0.5f, 0f, cz * 0.5f); // radius 0.5
            normals[i + 1] = Vector3.up;
            uvs[i + 1] = new Vector2(cx * 0.5f + 0.5f, cz * 0.5f + 0.5f);
        }

        var tris = new int[seg * 6];
        int t = 0;
        for (int i = 1; i <= seg; i++)
        {
            tris[t++] = 0; tris[t++] = i;     tris[t++] = i + 1; // top face
            tris[t++] = 0; tris[t++] = i + 1; tris[t++] = i;     // bottom face (reversed)
        }

        var mesh = new Mesh { name = "LiquidDisc" };
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
            AssetDatabase.CreateFolder("Assets", "Meshes");
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    static Material CreatePaintMaterial()
    {
        const string path = "Assets/Materials/LiquidPaint.mat";

        // Prefer our hand-written shader; fall back to URP/Lit only if it hasn't compiled yet.
        var shader = Shader.Find("Custom/LiquidPaint");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            if (shader != null && existing.shader != shader) existing.shader = shader; // swap to our shader
            return existing;
        }

        var m = new Material(shader);
        var paint = new Color(0.85f, 0.10f, 0.15f, 1f); // opaque red paint
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", paint);

        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        return m;
    }
}
