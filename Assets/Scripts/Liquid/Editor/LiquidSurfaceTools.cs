using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

// Editor-only setup tools for the liquid project (menu: Tools/Liquid/...).
// The old "flat liquid surface disc" approach (former Steps 0-3) has been removed:
// the fluid is now a hand-written SPH particle system (SphFluid) inside a procedurally
// generated bucket (ProceduralBucket). What remains here builds the SPH fluid, the tank,
// the canvas, the UI, the rope, and the procedural bucket.
public static class LiquidSurfaceTools
{
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

    [MenuItem("Tools/Liquid/Setup Demo Scene (positions + camera)")]
    static void SetupDemoScene()
    {
        SetTransform("ground", new Vector3(0, 0, 0), Vector3.zero, new Vector3(1000, 1, 1000));
        SetTransform("Bucket", new Vector3(0, 5, 0), Vector3.zero, new Vector3(7, 7, 7));
        SetTransform("PaintTank", new Vector3(5f, 1f, 0), Vector3.zero, new Vector3(1, 2, 1));
        SetTransform("Directional Light", new Vector3(0, 20, 0), new Vector3(50, -30, 0), Vector3.one);
        // Camera framed for runtime: bucket hangs ~ (0,3,0), tank at (5,1.5,0). 3/4 elevated front view.
        SetTransform("Main Camera", new Vector3(2.5f, 6f, 8f), new Vector3(24, 180, 0), Vector3.one);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Setup] Scene arranged (ground, Bucket, PaintTank, Light, Camera). Press Play.");
    }

    static void SetTransform(string name, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        var go = GameObject.Find(name);
        if (go == null) { Debug.LogWarning($"[Setup] '{name}' not found (skipped)."); return; }
        Undo.RecordObject(go.transform, "Setup Demo Scene");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        EditorUtility.SetDirty(go);
    }

    // Interview step (b): one click puts the scene in a guaranteed "show state" for the
    // painting demo — smooth particle count, wide swing, paint flowing immediately, side
    // tank disabled (it costs FPS and plays no role here), camera framed on bucket+canvas.
    // Values only; no logic is touched. Re-enable the tank objects to demo sloshing again.
    [MenuItem("Tools/Liquid/Demo - Painting Preset")]
    static void ApplyPaintingDemoPreset()
    {
        var bucketComp = Object.FindFirstObjectByType<Bucket>();
        if (bucketComp == null) { Debug.LogError("[Demo] No Bucket component found in the scene."); return; }

        SphFluid fluid = null;
        foreach (var f in Object.FindObjectsByType<SphFluid>(FindObjectsSortMode.None))
            if (f.containerShape == SphFluid.ContainerShape.Cylinder) { fluid = f; break; }
        if (fluid == null) { Debug.LogError("[Demo] No bucket SphFluid (Cylinder) found in the scene."); return; }

        // Wide, smooth pendulum swing.
        Undo.RecordObject(bucketComp, "Painting Demo Preset");
        bucketComp.enabled = true;
        bucketComp.useCircularMotion = false;
        bucketComp.l = 4f;
        bucketComp.thetaMax = 45f;
        bucketComp.omega = 1.5f;

        // Paint tuned for the show: light particle load, hole open, clear strokes.
        Undo.RecordObject(fluid, "Painting Demo Preset");
        fluid.particleCount = 500;
        fluid.holeOpen = true;
        fluid.holeDiameter = 0.2f;
        fluid.splatRadius = 0.18f;
        fluid.paintColor = new Color(0.85f, 0.10f, 0.15f, 1f);
        fluid.neighborMethod = SphFluid.NeighborMethod.SpatialHashGrid; // in case the O(n^2) demo left brute force on
        if (Application.isPlaying) fluid.Respawn(500);                  // apply immediately during play

        // The side tank costs FPS and plays no role in the painting act.
        foreach (var name in new[] { "TankFluid", "PaintTank" })
        {
            var go = GameObject.Find(name);
            if (go != null) { Undo.RecordObject(go, "Painting Demo Preset"); go.SetActive(false); }
        }

        // Known-good framing: bucket hanging center-frame, canvas below.
        SetTransform("Main Camera", new Vector3(2.5f, 6f, 8f), new Vector3(24f, 180f, 0f), Vector3.one);

        EditorSceneManager.MarkSceneDirty(bucketComp.gameObject.scene);
        Debug.Log("[Demo] Painting preset applied: 500 particles, hole open, grid mode, wide swing, " +
                  "side tank disabled, camera framed. Press Play.");
    }

    [MenuItem("Tools/Liquid/Tank - T1 Create Transparent Tank")]
    static void CreatePaintTank()
    {
        if (GameObject.Find("PaintTank") != null)
        {
            Debug.LogWarning("[Tank] 'PaintTank' already exists.");
            Selection.activeObject = GameObject.Find("PaintTank");
            return;
        }

        var go = new GameObject("PaintTank");
        Undo.RegisterCreatedObjectUndo(go, "Create Paint Tank");
        go.AddComponent<MeshFilter>().sharedMesh = CreateOrLoadBoxMesh();
        go.AddComponent<MeshRenderer>().sharedMaterial = CreateGlassMaterial();

        // A unit cube mesh scaled to the tank size, sitting on the ground at the side.
        go.transform.position = new Vector3(5f, 1f, 0f);
        go.transform.localScale = new Vector3(1f, 2f, 1f); // smaller tank => 300 particles fill it visibly

        Selection.activeObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[Tank][T1] Created transparent 'PaintTank' at the side. Next (T2) fills it with SPH.", go);
    }

    // Unit cube (size 1, centered) with flat per-face normals. Material renders both sides.
    static Mesh CreateOrLoadBoxMesh()
    {
        const string path = "Assets/Meshes/PaintTankBox.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        foreach (var nrm in faces)
        {
            Vector3 t = Vector3.Cross(nrm, Vector3.up);
            if (t.sqrMagnitude < 1e-4f) t = Vector3.Cross(nrm, Vector3.right);
            t.Normalize();
            Vector3 b = Vector3.Cross(nrm, t).normalized;
            Vector3 c = nrm * 0.5f;

            int baseIdx = verts.Count;
            verts.Add(c - t * 0.5f - b * 0.5f); norms.Add(nrm);
            verts.Add(c + t * 0.5f - b * 0.5f); norms.Add(nrm);
            verts.Add(c + t * 0.5f + b * 0.5f); norms.Add(nrm);
            verts.Add(c - t * 0.5f + b * 0.5f); norms.Add(nrm);
            tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
            tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
        }

        var mesh = new Mesh { name = "PaintTankBox" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        if (!AssetDatabase.IsValidFolder("Assets/Meshes")) AssetDatabase.CreateFolder("Assets", "Meshes");
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    // Transparent URP/Lit material (glass-like, both sides visible).
    static Material CreateGlassMaterial()
    {
        const string path = "Assets/Materials/GlassTank.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh);
        m.SetFloat("_Surface", 1f);   // transparent
        m.SetFloat("_Blend", 0f);     // alpha blend
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);      // render both faces
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.6f, 0.8f, 1f, 0.15f));

        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        return m;
    }

    [MenuItem("Tools/Liquid/Tank - T2 Fill With Liquid")]
    static void FillTank()
    {
        var tank = GameObject.Find("PaintTank");
        if (tank == null) { Debug.LogError("[Tank] Create the PaintTank first (T1)."); return; }
        if (GameObject.Find("TankFluid") != null)
        {
            Debug.LogWarning("[Tank] 'TankFluid' already exists.");
            Selection.activeObject = GameObject.Find("TankFluid");
            return;
        }

        var go = new GameObject("TankFluid");
        Undo.RegisterCreatedObjectUndo(go, "Create Tank Fluid");
        var sph = go.AddComponent<SphFluid>();
        sph.bucket = tank.transform;
        sph.containerShape = SphFluid.ContainerShape.Box;
        sph.particleMaterial = CreatePaintMaterial();
        sph.particleCount = 300;
        sph.holeOpen = false;
        sph.showStats = false; // avoid overlapping the bucket fluid's HUD

        Selection.activeObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[Tank][T2] Created 'TankFluid' (Box) filling the PaintTank. Press Play.", go);
    }

    [MenuItem("Tools/Liquid/Tank - T3 Add Shake")]
    static void AddTankShake()
    {
        var tank = GameObject.Find("PaintTank");
        if (tank == null) { Debug.LogError("[Tank] Create the PaintTank first (T1)."); return; }
        if (tank.GetComponent<Shaker>() != null)
        {
            Debug.LogWarning("[Tank] Shaker already attached.");
            Selection.activeObject = tank;
            return;
        }

        Undo.AddComponent<Shaker>(tank);
        Selection.activeObject = tank;
        EditorSceneManager.MarkSceneDirty(tank.scene);
        Debug.Log("[Tank][T3] Added Shaker to PaintTank. Press Play -> the tank rocks and the liquid sloshes.", tank);
    }

    [MenuItem("Tools/Liquid/Canvas - M4.1 Create Canvas")]
    static void CreateCanvas()
    {
        if (GameObject.Find("PaintCanvas") != null)
        {
            Debug.LogWarning("[Canvas] 'PaintCanvas' already exists.");
            Selection.activeObject = GameObject.Find("PaintCanvas");
            return;
        }

        var go = new GameObject("PaintCanvas");
        Undo.RegisterCreatedObjectUndo(go, "Create Paint Canvas");
        go.AddComponent<MeshFilter>().sharedMesh = CreateOrLoadQuadMesh();
        go.AddComponent<MeshRenderer>().sharedMaterial = CreateCanvasMaterial();
        go.AddComponent<PaintCanvas>();

        // Horizontal canvas just above the ground, centered under the bucket's swing.
        go.transform.position = new Vector3(0f, 0.05f, 0f);
        go.transform.localScale = new Vector3(6f, 1f, 6f);

        Selection.activeObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[Canvas][M4.1] Created a blank PaintCanvas under the bucket.", go);
    }

    // Flat quad in local XZ (normal +Y, UV 0..1) — the painting surface.
    static Mesh CreateOrLoadQuadMesh()
    {
        const string path = "Assets/Meshes/CanvasQuad.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        var m = new Mesh { name = "CanvasQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f)
        };
        m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();

        if (!AssetDatabase.IsValidFolder("Assets/Meshes")) AssetDatabase.CreateFolder("Assets", "Meshes");
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        return m;
    }

    static Material CreateCanvasMaterial()
    {
        const string path = "Assets/Materials/PaintCanvas.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh);
        m.SetFloat("_Cull", 0f); // double-sided so the canvas always shows
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white); // let the texture show
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        return m;
    }

    [MenuItem("Tools/Liquid/Canvas - M4.2 Link Bucket Paint To Canvas")]
    static void LinkPaintToCanvas()
    {
        var canvas = Object.FindFirstObjectByType<PaintCanvas>();
        if (canvas == null) { Debug.LogError("[Canvas] Create the PaintCanvas first (M4.1)."); return; }

        // Find the bucket's fluid (Cylinder shape), not the tank's (Box).
        var fluids = Object.FindObjectsByType<SphFluid>(FindObjectsSortMode.None);
        SphFluid bucketFluid = null;
        foreach (var f in fluids)
            if (f.containerShape == SphFluid.ContainerShape.Cylinder) { bucketFluid = f; break; }
        if (bucketFluid == null) { Debug.LogError("[Canvas] No bucket SphFluid (Cylinder) found."); return; }

        Undo.RecordObject(bucketFluid, "Link Canvas");
        bucketFluid.paintCanvas = canvas;
        bucketFluid.holeOpen = true; // paint must flow out to draw
        EditorUtility.SetDirty(bucketFluid);
        EditorSceneManager.MarkSceneDirty(bucketFluid.gameObject.scene);
        Debug.Log("[Canvas][M4.2] Linked bucket paint -> canvas, hole opened. Press Play and watch marks appear.", bucketFluid);
    }

    [MenuItem("Tools/Liquid/UI - M5.1 Create Control Panel")]
    static void CreateControlPanel()
    {
        if (GameObject.Find("ControlPanelUI") != null)
        {
            Debug.LogWarning("[UI] 'ControlPanelUI' already exists.");
            Selection.activeObject = GameObject.Find("ControlPanelUI");
            return;
        }

        // An EventSystem is required for any UI interaction (clicks/drags on sliders).
        EnsureEventSystem();

        // Root Canvas (screen-space overlay for now; switching to World Space later = one field).
        var canvasGO = new GameObject("ControlPanelUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Control Panel");
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f; // match HEIGHT so the tall panel always fits vertically

        // A semi-transparent panel anchored to the top-left, ready to receive controls in M5.2.
        var panelGO = new GameObject("Panel", typeof(Image));
        Undo.SetTransformParent(panelGO.transform, canvasGO.transform, "Parent Panel");
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 1f);
        panelRT.anchorMax = new Vector2(0f, 1f);
        panelRT.pivot = new Vector2(0f, 1f);
        panelRT.anchoredPosition = new Vector2(20f, -20f);
        panelRT.sizeDelta = new Vector2(360f, 520f);
        panelGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // The binding script, with references auto-filled from the scene.
        var ctrl = canvasGO.AddComponent<UIControlPanel>();
        ctrl.bucket = Object.FindFirstObjectByType<Bucket>();
        ctrl.canvas = Object.FindFirstObjectByType<PaintCanvas>();
        foreach (var f in Object.FindObjectsByType<SphFluid>(FindObjectsSortMode.None))
            if (f.containerShape == SphFluid.ContainerShape.Cylinder) { ctrl.bucketFluid = f; break; }

        Selection.activeObject = canvasGO;
        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Debug.Log("[UI][M5.1] Created 'ControlPanelUI' (Canvas + empty Panel). " +
                  "Press Play: an empty dark panel should appear top-left. M5.2 fills it with sliders.", canvasGO);
    }

    [MenuItem("Tools/Liquid/UI - Fix EventSystem Input Module")]
    static void FixEventSystemInputModule()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null) { Debug.LogWarning("[UI] No EventSystem in the scene. Run M5.1 first."); return; }
        ApplyCorrectInputModule(es.gameObject);
        Selection.activeObject = es.gameObject;
        EditorSceneManager.MarkSceneDirty(es.gameObject.scene);
    }

    // Create an EventSystem (if missing) with the input module that matches the project's
    // active input handling. Projects using the new Input System package must NOT use the
    // legacy StandaloneInputModule (it reads UnityEngine.Input and throws every frame).
    static void EnsureEventSystem()
    {
        var existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing != null) { ApplyCorrectInputModule(existing.gameObject); return; }

        var es = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        ApplyCorrectInputModule(es);
    }

    // Pick the right UI input module without a hard compile-time dependency on the
    // Input System package (resolved by name, so this file still compiles if it's absent).
    static void ApplyCorrectInputModule(GameObject go)
    {
        var newModuleType = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

        if (newModuleType != null)
        {
            // Remove the legacy module if present, then ensure the new one exists.
            var legacy = go.GetComponent<StandaloneInputModule>();
            if (legacy != null) Undo.DestroyObjectImmediate(legacy);
            if (go.GetComponent(newModuleType) == null)
                Undo.AddComponent(go, newModuleType);
            Debug.Log("[UI] EventSystem uses InputSystemUIInputModule (new Input System).", go);
        }
        else if (go.GetComponent<StandaloneInputModule>() == null)
        {
            Undo.AddComponent<StandaloneInputModule>(go);
            Debug.Log("[UI] EventSystem uses StandaloneInputModule (legacy input).", go);
        }
    }

    [MenuItem("Tools/Liquid/UI - M5.2 Add Physics Sliders")]
    static void AddPhysicsSliders()
    {
        var ctrl = Object.FindFirstObjectByType<UIControlPanel>();
        if (ctrl == null) { Debug.LogError("[UI][M5.2] No ControlPanelUI in the scene. Run M5.1 first."); return; }
        var panel = ctrl.transform.Find("Panel") as RectTransform;
        if (panel == null) { Debug.LogError("[UI][M5.2] 'Panel' not found under ControlPanelUI."); return; }
        // Rebuild on re-run so tweaks (font size, ranges) re-apply cleanly.
        foreach (var n in new[] { "RopeLengthRow", "ReleaseAngleRow", "SpeedRow", "SwingCountRow" })
        {
            var old = panel.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        // Dark translucent panel (preferred look); white bold labels read clearly on it.
        var panelImg = panel.GetComponent<Image>();
        if (panelImg != null) panelImg.color = new Color(0f, 0f, 0f, 0.55f);

        if (ctrl.bucket == null) ctrl.bucket = Object.FindFirstObjectByType<Bucket>();
        var bucket = ctrl.bucket;
        float l   = bucket != null ? bucket.l        : 4f;
        float ang = bucket != null ? bucket.thetaMax : 45f;
        float spd = bucket != null ? bucket.omega    : 1.5f;

        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Add Physics Sliders");

        ctrl.ropeSlider  = BuildControlRow(panel, "Rope Length",   1f, 8f,  l,   0, out ctrl.ropeValue);
        ctrl.angleSlider = BuildControlRow(panel, "Release Angle", 0f, 90f, ang, 1, out ctrl.angleValue);
        ctrl.speedSlider = BuildControlRow(panel, "Speed",         0f, 3f,  spd, 2, out ctrl.speedValue);
        // Swing count: whole numbers, 0..10 (0 = unlimited).
        int swings = bucket != null ? bucket.swingCount : 0;
        ctrl.swingSlider = BuildControlRow(panel, "Swing Count",   0f, 10f, swings, 3, out ctrl.swingValue);
        ctrl.swingSlider.wholeNumbers = true;

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[UI][M5.2] Added 3 physics sliders (rope length, angle, speed) wired to UIControlPanel.", ctrl.gameObject);
    }

    [MenuItem("Tools/Liquid/UI - M5.3 Add Liquid Sliders")]
    static void AddLiquidSliders()
    {
        var ctrl = Object.FindFirstObjectByType<UIControlPanel>();
        if (ctrl == null) { Debug.LogError("[UI][M5.3] No ControlPanelUI in the scene. Run M5.1 first."); return; }
        var panel = ctrl.transform.Find("Panel") as RectTransform;
        if (panel == null) { Debug.LogError("[UI][M5.3] 'Panel' not found under ControlPanelUI."); return; }

        // Link the bucket's fluid (Cylinder shape), not the tank's (Box).
        if (ctrl.bucketFluid == null)
            foreach (var f in Object.FindObjectsByType<SphFluid>(FindObjectsSortMode.None))
                if (f.containerShape == SphFluid.ContainerShape.Cylinder) { ctrl.bucketFluid = f; break; }
        var fluid = ctrl.bucketFluid;
        if (fluid == null) { Debug.LogError("[UI][M5.3] No bucket SphFluid (Cylinder) found in the scene."); return; }

        // Rebuild on re-run so tweaks re-apply cleanly.
        foreach (var n in new[] { "ViscosityRow", "HoleDiameterRow", "SplatWidthRow" })
        {
            var old = panel.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        // Indices 4..6 stack these below the physics rows (0..3 from M5.2: rope, angle, speed, swing).
        ctrl.viscositySlider = BuildControlRow(panel, "Viscosity",     0f,    20f, fluid.viscosity,    4, out ctrl.viscosityValue);
        ctrl.holeSlider      = BuildControlRow(panel, "Hole Diameter", 0.05f, 1f,  fluid.holeDiameter, 5, out ctrl.holeValue);
        ctrl.splatSlider     = BuildControlRow(panel, "Splat Width",   0.05f, 0.5f, fluid.splatRadius, 6, out ctrl.splatValue);

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[UI][M5.3] Added 3 liquid sliders (viscosity, hole diameter, splat width) wired to UIControlPanel.", ctrl.gameObject);
    }

    [MenuItem("Tools/Liquid/UI - M5.4a Add Color Controls")]
    static void AddColorControls()
    {
        var ctrl = Object.FindFirstObjectByType<UIControlPanel>();
        if (ctrl == null) { Debug.LogError("[UI][M5.4a] No ControlPanelUI in the scene. Run M5.1 first."); return; }
        var panel = ctrl.transform.Find("Panel") as RectTransform;
        if (panel == null) { Debug.LogError("[UI][M5.4a] 'Panel' not found under ControlPanelUI."); return; }

        if (ctrl.bucketFluid == null)
            foreach (var f in Object.FindObjectsByType<SphFluid>(FindObjectsSortMode.None))
                if (f.containerShape == SphFluid.ContainerShape.Cylinder) { ctrl.bucketFluid = f; break; }
        var fluid = ctrl.bucketFluid;
        if (fluid == null) { Debug.LogError("[UI][M5.4a] No bucket SphFluid (Cylinder) found."); return; }

        // Grow the panel so the color section fits below the existing 6 rows.
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, 820f);

        foreach (var n in new[] { "PaintColorRow", "RedRow", "GreenRow", "BlueRow" })
        {
            var old = panel.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        BuildColorSwatches(panel, ctrl, 6);                 // preset buttons (row 6)

        Color c = fluid.paintColor;                          // RGB sliders (rows 7..9)
        ctrl.rSlider = BuildControlRow(panel, "Red",   0f, 1f, c.r, 7, out ctrl.rValue);
        ctrl.gSlider = BuildControlRow(panel, "Green", 0f, 1f, c.g, 8, out ctrl.gValue);
        ctrl.bSlider = BuildControlRow(panel, "Blue",  0f, 1f, c.b, 9, out ctrl.bValue);

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[UI][M5.4a] Added color swatches + RGB sliders wired to UIControlPanel.", ctrl.gameObject);
    }

    [MenuItem("Tools/Liquid/UI - M5.4b Add Action Buttons")]
    static void AddActionButtons()
    {
        var ctrl = Object.FindFirstObjectByType<UIControlPanel>();
        if (ctrl == null) { Debug.LogError("[UI][M5.4b] No ControlPanelUI in the scene. Run M5.1 first."); return; }
        var panel = ctrl.transform.Find("Panel") as RectTransform;
        if (panel == null) { Debug.LogError("[UI][M5.4b] 'Panel' not found under ControlPanelUI."); return; }

        if (ctrl.bucketFluid == null)
            foreach (var f in Object.FindObjectsByType<SphFluid>(FindObjectsSortMode.None))
                if (f.containerShape == SphFluid.ContainerShape.Cylinder) { ctrl.bucketFluid = f; break; }
        if (ctrl.canvas == null) ctrl.canvas = Object.FindFirstObjectByType<PaintCanvas>();

        // Grow the panel to fit the buttons row, then (re)build it.
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, 900f);
        var oldRow = panel.Find("ActionsRow");
        if (oldRow != null) Undo.DestroyObjectImmediate(oldRow.gameObject);

        const float top = 14f, pad = 12f, rowH = 80f, innerW = 336f, gap = 8f, btnH = 40f;
        var row = new GameObject("ActionsRow", typeof(RectTransform));
        var rrt = row.GetComponent<RectTransform>();
        rrt.SetParent(panel, false);
        rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
        rrt.pivot = new Vector2(0f, 1f);
        rrt.anchoredPosition = new Vector2(pad, -(top + 10 * rowH));
        rrt.sizeDelta = new Vector2(innerW, btnH);

        float btnW = (innerW - gap) / 2f;
        ctrl.holeButton = NewButton(rrt, "Hole", 0f, 0f, btnW, btnH, new Color(0.20f, 0.40f, 0.60f, 1f), out ctrl.holeLabel);
        ctrl.holeButton.name = "HoleButton";
        ctrl.clearButton = NewButton(rrt, "Clear Canvas", btnW + gap, 0f, btnW, btnH, new Color(0.60f, 0.25f, 0.25f, 1f), out _);
        ctrl.clearButton.name = "ClearButton";

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[UI][M5.4b] Added Hole toggle + Clear Canvas buttons wired to UIControlPanel.", ctrl.gameObject);
    }

    [MenuItem("Tools/Liquid/UI - M5.5 Add Save/Reset Buttons")]
    static void AddSaveResetButtons()
    {
        var ctrl = Object.FindFirstObjectByType<UIControlPanel>();
        if (ctrl == null) { Debug.LogError("[UI][M5.5] No ControlPanelUI in the scene. Run M5.1 first."); return; }
        var panel = ctrl.transform.Find("Panel") as RectTransform;
        if (panel == null) { Debug.LogError("[UI][M5.5] 'Panel' not found under ControlPanelUI."); return; }
        if (ctrl.canvas == null) ctrl.canvas = Object.FindFirstObjectByType<PaintCanvas>();

        // Grow the panel for the last row, then (re)build it.
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, 960f);
        var oldRow = panel.Find("SaveResetRow");
        if (oldRow != null) Undo.DestroyObjectImmediate(oldRow.gameObject);

        const float top = 14f, pad = 12f, rowH = 80f, innerW = 336f, gap = 8f, btnH = 40f;
        var row = new GameObject("SaveResetRow", typeof(RectTransform));
        var rrt = row.GetComponent<RectTransform>();
        rrt.SetParent(panel, false);
        rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
        rrt.pivot = new Vector2(0f, 1f);
        rrt.anchoredPosition = new Vector2(pad, -(top + 11 * rowH));
        rrt.sizeDelta = new Vector2(innerW, btnH);

        float btnW = (innerW - gap) / 2f;
        ctrl.saveButton  = NewButton(rrt, "Save PNG", 0f,         0f, btnW, btnH, new Color(0.20f, 0.55f, 0.30f, 1f), out _);
        ctrl.saveButton.name = "SaveButton";
        ctrl.resetButton = NewButton(rrt, "Reset",    btnW + gap, 0f, btnW, btnH, new Color(0.45f, 0.45f, 0.45f, 1f), out _);
        ctrl.resetButton.name = "ResetButton";

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[UI][M5.5] Added Save PNG + Reset buttons wired to UIControlPanel.", ctrl.gameObject);
    }

    [MenuItem("Tools/Liquid/UI - S3 Add Complexity Controls")]
    static void AddComplexityControls()
    {
        var ctrl = Object.FindFirstObjectByType<UIControlPanel>();
        if (ctrl == null) { Debug.LogError("[UI][S3] No ControlPanelUI in the scene. Run M5.1 first."); return; }

        if (ctrl.bucketFluid == null)
            foreach (var f in Object.FindObjectsByType<SphFluid>(FindObjectsSortMode.None))
                if (f.containerShape == SphFluid.ContainerShape.Cylinder) { ctrl.bucketFluid = f; break; }
        if (ctrl.bucketFluid == null) { Debug.LogError("[UI][S3] No bucket SphFluid (Cylinder) found."); return; }

        // A separate panel, top-right, so the main controls panel stays intact.
        var old = ctrl.transform.Find("DemoPanel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var panelGO = new GameObject("DemoPanel", typeof(Image));
        var prt = (RectTransform)panelGO.transform;
        prt.SetParent(ctrl.transform, false);
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 1f);
        prt.pivot = new Vector2(1f, 1f);
        prt.anchoredPosition = new Vector2(-20f, -20f);
        prt.sizeDelta = new Vector2(340f, 200f);
        panelGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.82f);

        var blue = new Color(0.10f, 0.35f, 0.85f, 1f);        // clear blue text
        var btnBg = new Color(0.86f, 0.91f, 1f, 1f);          // light button so blue reads

        NewLabel(prt, "SPH Complexity", 12f, 8f, 316f, 26f, TextAnchor.MiddleLeft).color = blue;

        ctrl.methodButton = NewButton(prt, "Mode", 12f, 40f, 316f, 36f, btnBg, out ctrl.methodLabel);
        ctrl.methodLabel.color = blue;
        ctrl.methodButton.name = "MethodButton";

        NewLabel(prt, "Particles", 12f, 84f, 190f, 24f, TextAnchor.MiddleLeft).color = blue;
        ctrl.particleValue = NewLabel(prt, ctrl.bucketFluid.ParticleCount.ToString(), 216f, 84f, 112f, 24f, TextAnchor.MiddleRight);
        ctrl.particleValue.color = blue;
        ctrl.particleSlider = NewSlider(prt, 12f, 110f, 316f, 20f, 200f, 3000f, ctrl.bucketFluid.ParticleCount);

        ctrl.applyButton = NewButton(prt, "Apply", 12f, 140f, 150f, 36f, btnBg, out var applyLabel);
        applyLabel.color = blue;
        ctrl.applyButton.name = "ApplyButton";
        ctrl.statsReadout = NewLabel(prt, "-", 170f, 140f, 158f, 36f, TextAnchor.MiddleLeft);
        ctrl.statsReadout.color = blue;
        ctrl.statsReadout.resizeTextForBestFit = true;
        ctrl.statsReadout.resizeTextMinSize = 8;
        ctrl.statsReadout.resizeTextMaxSize = 20;

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[UI][S3] Added complexity demo panel (mode toggle, particle count, apply, live ms).", ctrl.gameObject);
    }

    // Interview step (c1): the remaining PDF inputs — gravity, wall bounce (friction-like),
    // canvas size, and the motion mode (pendulum/circular, already in Bucket.cs but never
    // exposed). Built as a separate right-side panel so the main panel layout is untouched.
    [MenuItem("Tools/Liquid/UI - C1 Add Environment Controls")]
    static void AddEnvironmentControls()
    {
        var ctrl = Object.FindFirstObjectByType<UIControlPanel>();
        if (ctrl == null) { Debug.LogError("[UI][C1] No ControlPanelUI in the scene. Run M5.1 first."); return; }

        if (ctrl.bucket == null) ctrl.bucket = Object.FindFirstObjectByType<Bucket>();
        if (ctrl.canvas == null) ctrl.canvas = Object.FindFirstObjectByType<PaintCanvas>();
        if (ctrl.bucketFluid == null)
            foreach (var f in Object.FindObjectsByType<SphFluid>(FindObjectsSortMode.None))
                if (f.containerShape == SphFluid.ContainerShape.Cylinder) { ctrl.bucketFluid = f; break; }
        if (ctrl.bucketFluid == null) { Debug.LogError("[UI][C1] No bucket SphFluid (Cylinder) found."); return; }

        var old = ctrl.transform.Find("EnvPanel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Below the complexity demo panel (top-right at -20..-220).
        var panelGO = new GameObject("EnvPanel", typeof(Image));
        var prt = (RectTransform)panelGO.transform;
        prt.SetParent(ctrl.transform, false);
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 1f);
        prt.pivot = new Vector2(1f, 1f);
        prt.anchoredPosition = new Vector2(-20f, -232f);
        prt.sizeDelta = new Vector2(360f, 432f);   // 6 compact rows + motion button
        panelGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f); // dark like the main panel

        NewLabel(prt, "Environment & Scene", 12f, 8f, 316f, 26f, TextAnchor.MiddleLeft);

        var fluid = ctrl.bucketFluid;
        float canvasSize = ctrl.canvas != null ? ctrl.canvas.transform.localScale.x : 6f;
        float canvasTilt = ctrl.canvas != null ? NormalizeTiltEuler(ctrl.canvas.transform.localEulerAngles.x) : 0f;
        const float baseY = 40f;   // below the title
        ctrl.gravitySlider    = BuildCompactRow(prt, baseY, 0, "Gravity",         0f, 20f, -fluid.gravity.y,      out ctrl.gravityValue);
        ctrl.bounceSlider     = BuildCompactRow(prt, baseY, 1, "Wall Bounce",     0f, 1f,  fluid.boundaryDamping, out ctrl.bounceValue,  "0.00");
        ctrl.canvasSizeSlider = BuildCompactRow(prt, baseY, 2, "Canvas Size",     2f, 10f, canvasSize,            out ctrl.canvasSizeValue);
        ctrl.canvasTiltSlider = BuildCompactRow(prt, baseY, 3, "Canvas Tilt",     0f, 60f, canvasTilt,            out ctrl.canvasTiltValue);
        ctrl.airSlider        = BuildCompactRow(prt, baseY, 4, "Air Resistance",  0f, 1f,  0f,                    out ctrl.airValue,     "0.00");
        ctrl.humiditySlider   = BuildCompactRow(prt, baseY, 5, "Humidity",        0f, 1f,  fluid.humidity,        out ctrl.humidityValue, "0.00");

        // Two half-width buttons share one row: Motion (left) and Surface (right).
        float btnY = baseY + 6 * 56f + 4f;
        ctrl.motionButton = NewButton(prt, "Motion", 12f, btnY, 160f, 36f, new Color(0.20f, 0.40f, 0.60f, 1f), out ctrl.motionLabel);
        ctrl.motionButton.name = "MotionButton";
        ctrl.motionLabel.text = ctrl.bucket != null && ctrl.bucket.useCircularMotion ? "Motion: Circular" : "Motion: Pendulum";
        ctrl.motionLabel.fontSize = 16;
        ctrl.motionLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

        ctrl.surfaceButton = NewButton(prt, "Surface", 188f, btnY, 160f, 36f, new Color(0.30f, 0.45f, 0.35f, 1f), out ctrl.surfaceLabel);
        ctrl.surfaceButton.name = "SurfaceButton";
        ctrl.surfaceLabel.text = "Surface: Canvas";
        ctrl.surfaceLabel.fontSize = 16;
        ctrl.surfaceLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

        // Dark panel -> make every label readable immediately (and avoid the short-rect hide bug).
        foreach (var t in prt.GetComponentsInChildren<Text>(true))
        { t.color = Color.white; t.verticalOverflow = VerticalWrapMode.Overflow; }

        if (ctrl.canvas == null)
            Debug.LogWarning("[UI][C1] No PaintCanvas found — the Canvas Size/Tilt sliders will be inert until one exists.");

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[UI][C1] Added Environment panel (gravity, wall bounce, canvas size, motion toggle).", ctrl.gameObject);
    }

    // Interview step (c2): visual polish. Adds a title + section headers to the main panel
    // (repositioning the existing rows by name), restyles the Environment panel to the dark
    // theme so its white labels are readable, and re-asserts the demo panel's blue labels.
    // Layout only — no behaviour changes.
    [MenuItem("Tools/Liquid/UI - C2 Polish Panels")]
    static void PolishPanels()
    {
        var ctrl = Object.FindFirstObjectByType<UIControlPanel>();
        if (ctrl == null) { Debug.LogError("[UI][C2] No ControlPanelUI in the scene. Run M5.1 first."); return; }
        var panel = ctrl.transform.Find("Panel") as RectTransform;
        if (panel == null) { Debug.LogError("[UI][C2] 'Panel' not found under ControlPanelUI."); return; }

        Undo.RegisterFullObjectHierarchyUndo(ctrl.gameObject, "Polish Panels");

        // Scale the UI by window HEIGHT so the tall panel always fits, even in short views.
        var scaler = ctrl.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.matchWidthOrHeight = 1f;

        // --- 1) Main panel: title + section headers, rows shifted to make room ---
        foreach (var n in new[] { "PanelTitle", "MotionHeader", "LiquidHeader", "ColorHeader" })
        {
            var o = panel.Find(n);
            if (o != null) Undo.DestroyObjectImmediate(o.gameObject);
        }

        var title = NewLabel(panel, "Paint Controls", 12f, 10f, 336f, 30f, TextAnchor.MiddleCenter);
        title.name = "PanelTitle";

        var headerColor = new Color(0.55f, 0.78f, 1f, 1f);
        void Header(string name, string text, float y)
        {
            var h = NewLabel(panel, text, 12f, y, 336f, 24f, TextAnchor.MiddleLeft);
            h.name = name; h.color = headerColor; h.fontSize = 20;
        }
        Header("MotionHeader", "Motion", 44f);
        Header("LiquidHeader", "Liquid", 366f);
        Header("ColorHeader",  "Color",  614f);

        // Reposition every existing row under its section header. Spacing is compacted to 74px
        // so the extra Swing Count row fits without pushing Save/Reset off the bottom.
        SetRowY(panel, "RopeLengthRow",   70f);
        SetRowY(panel, "ReleaseAngleRow", 144f);
        SetRowY(panel, "SpeedRow",        218f);
        SetRowY(panel, "SwingCountRow",   292f);
        SetRowY(panel, "ViscosityRow",    392f);
        SetRowY(panel, "HoleDiameterRow", 466f);
        SetRowY(panel, "SplatWidthRow",   540f);
        SetRowY(panel, "PaintColorRow",   640f);
        SetRowY(panel, "RedRow",          714f);
        SetRowY(panel, "GreenRow",        788f);
        SetRowY(panel, "BlueRow",         862f);
        SetRowY(panel, "ActionsRow",      936f);
        SetRowY(panel, "SaveResetRow",    984f);
        panel.sizeDelta = new Vector2(360f, 1030f);

        // --- 2) Environment panel: dark theme + readable labels ---
        var env = ctrl.transform.Find("EnvPanel") as RectTransform;
        if (env != null)
        {
            env.sizeDelta = new Vector2(360f, 432f);
            env.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            foreach (var t in env.GetComponentsInChildren<Text>(true))
            { t.color = Color.white; t.verticalOverflow = VerticalWrapMode.Overflow; }
            // Motion + Surface share one half-width row; recolour without resizing (C1 sets sizes).
            var mb = env.Find("MotionButton");
            if (mb != null) mb.GetComponent<Image>().color = new Color(0.20f, 0.40f, 0.60f, 1f);
            var sb2 = env.Find("SurfaceButton");
            if (sb2 != null) sb2.GetComponent<Image>().color = new Color(0.30f, 0.45f, 0.35f, 1f);
        }

        // --- 3) Demo panel: make sure its labels are the blue theme (idempotent) ---
        var demo = ctrl.transform.Find("DemoPanel") as RectTransform;
        if (demo != null)
        {
            var blue = new Color(0.10f, 0.35f, 0.85f, 1f);
            foreach (var t in demo.GetComponentsInChildren<Text>(true))
            { t.color = blue; t.verticalOverflow = VerticalWrapMode.Overflow; }
        }

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[UI][C2] Panels polished: title + section headers, dark Environment panel, blue demo labels.");
    }

    static void SetRowY(RectTransform panel, string name, float y)
    {
        var r = panel.Find(name) as RectTransform;
        if (r != null) r.anchoredPosition = new Vector2(12f, -y);
        else Debug.LogWarning("[UI][C2] Row '" + name + "' not found (skipped) — run its build menu first.");
    }

    // M6.3 (PDF output 6): a right-side panel showing "previous -> current" experiment
    // values and results, refreshed live by UIControlPanel. Saving an experiment makes it
    // the "previous" column.
    [MenuItem("Tools/Liquid/UI - M6.3 Add Comparison Panel")]
    static void AddComparisonPanel()
    {
        var ctrl = Object.FindFirstObjectByType<UIControlPanel>();
        if (ctrl == null) { Debug.LogError("[UI][M6.3] No ControlPanelUI in the scene. Run M5.1 first."); return; }

        var old = ctrl.transform.Find("ComparePanel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Below the Environment panel (top -240, height 376 -> ends at -616).
        var panelGO = new GameObject("ComparePanel", typeof(Image));
        var prt = (RectTransform)panelGO.transform;
        prt.SetParent(ctrl.transform, false);
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 1f);
        prt.pivot = new Vector2(1f, 1f);
        prt.anchoredPosition = new Vector2(-20f, -684f);
        prt.sizeDelta = new Vector2(360f, 390f);
        panelGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        NewLabel(prt, "Experiment Comparison", 12f, 8f, 336f, 26f, TextAnchor.MiddleLeft);

        var txt = NewLabel(prt, "-", 12f, 40f, 336f, 342f, TextAnchor.UpperLeft);
        txt.name = "ComparisonText";
        txt.fontSize = 18;
        txt.fontStyle = FontStyle.Normal;
        ctrl.comparisonText = txt;

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        Debug.Log("[UI][M6.3] Added the Experiment Comparison panel (previous -> current).", ctrl.gameObject);
    }

    [MenuItem("Tools/Liquid/Rope - Add Visible Rope")]
    static void AddVisibleRope()
    {
        var bucket = Object.FindFirstObjectByType<Bucket>();
        if (bucket == null) { Debug.LogError("[Rope] No Bucket found in the scene."); return; }

        // Rebuild cleanly on re-run.
        var existing = GameObject.Find("Rope");
        if (existing != null && existing.GetComponent<Rope>() != null) Undo.DestroyObjectImmediate(existing);
        var existingAnchor = GameObject.Find("RopeAnchor");
        if (existingAnchor != null) Undo.DestroyObjectImmediate(existingAnchor);

        var sh = Shader.Find("Universal Render Pipeline/Lit");

        // Built-in cylinder mesh, but strip the collider — constraint: no physics colliders.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Rope";
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        if (sh != null)
        {
            var mat = new Material(sh);
            var brown = new Color(0.35f, 0.24f, 0.12f, 1f);
            mat.SetColor("_BaseColor", brown); mat.color = brown;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // Visible suspension point (anchor) — a small dark sphere pinned at the pivot.
        var anchor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        anchor.name = "RopeAnchor";
        var acol = anchor.GetComponent<Collider>();
        if (acol != null) Object.DestroyImmediate(acol);
        anchor.transform.localScale = Vector3.one * 0.15f;
        if (sh != null)
        {
            var amat = new Material(sh);
            var dark = new Color(0.15f, 0.15f, 0.17f, 1f);
            amat.SetColor("_BaseColor", dark); amat.color = dark;
            anchor.GetComponent<MeshRenderer>().sharedMaterial = amat;
        }

        var rope = go.AddComponent<Rope>();
        rope.bucket = bucket;
        rope.anchor = anchor.transform;

        Undo.RegisterCreatedObjectUndo(go, "Add Visible Rope");
        Undo.RegisterCreatedObjectUndo(anchor, "Add Rope Anchor");
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[Rope] Added a visible rope + suspension anchor (no colliders) wired to the Bucket. " +
                  "Constant length; fully visible in Play mode.", go);
    }

    [MenuItem("Tools/Liquid/Bucket - Build Procedural Bucket")]
    static void BuildProceduralBucket()
    {
        var bucketComp = Object.FindFirstObjectByType<Bucket>();
        if (bucketComp == null) { Debug.LogError("[Bucket] No Bucket found in the scene."); return; }
        var go = bucketComp.gameObject;

        Undo.RegisterFullObjectHierarchyUndo(go, "Build Procedural Bucket");

        // Unit scale so mesh units == world units (predictable size).
        go.transform.localScale = Vector3.one;

        var mf = go.GetComponent<MeshFilter>();   if (mf == null) mf = Undo.AddComponent<MeshFilter>(go);
        var mr = go.GetComponent<MeshRenderer>(); if (mr == null) mr = Undo.AddComponent<MeshRenderer>(go);

        // Hide any leftover child renderers from the old imported model.
        foreach (var childMr in go.GetComponentsInChildren<MeshRenderer>(true))
            if (childMr.gameObject != go) childMr.enabled = false;

        // Semi-transparent, double-sided URP material so the liquid inside stays visible.
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(sh);
        mat.SetFloat("_Surface", 1f);                 // transparent
        mat.SetFloat("_Blend", 0f);                   // alpha blend
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f);                    // double-sided
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", new Color(0.55f, 0.62f, 0.72f, 0.35f));
        mr.sharedMaterial = mat;

        // Add the generator and wire it to the bucket's fluid.
        var pb = go.GetComponent<ProceduralBucket>(); if (pb == null) pb = Undo.AddComponent<ProceduralBucket>(go);
        if (pb.fluid == null)
            foreach (var f in Object.FindObjectsByType<SphFluid>(FindObjectsSortMode.None))
                if (f.containerShape == SphFluid.ContainerShape.Cylinder) { pb.fluid = f; break; }

        // Make the liquid nearly incompressible so it does NOT collapse into a smaller blob after
        // spawning (the realistic look). Affordable now the bucket holds far fewer particles: we
        // raise stiffness and take smaller, more numerous sub-steps for stability.
        if (pb.fluid != null)
        {
            pb.fluid.stiffness = 8f;      // firmer than the default 3, but stable when the bucket swings
            pb.fluid.timeStep = 0.003f;
            pb.fluid.maxSubSteps = 6;
        }
        pb.Rebuild();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[Bucket] Built a procedural transparent bucket (open cylinder) and matched the SPH " +
                  "container to it. Tune Top/Bottom Radius + Height on the ProceduralBucket component.", go);
    }

    // A "Paint Color" row: a left label plus a strip of clickable preset color swatches.
    static void BuildColorSwatches(RectTransform panel, UIControlPanel ctrl, int index)
    {
        const float rowH = 80f, top = 14f, pad = 12f, innerW = 336f;
        var row = new GameObject("PaintColorRow", typeof(RectTransform));
        var rrt = row.GetComponent<RectTransform>();
        rrt.SetParent(panel, false);
        rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
        rrt.pivot = new Vector2(0f, 1f);
        rrt.anchoredPosition = new Vector2(pad, -(top + index * rowH));
        rrt.sizeDelta = new Vector2(innerW, rowH - 8f);

        NewLabel(rrt, "Paint Color", 0f, 0f, 236f, 32f, TextAnchor.MiddleLeft);

        Color[] presets =
        {
            new Color(0.85f, 0.10f, 0.15f), // red
            new Color(0.10f, 0.35f, 0.90f), // blue
            new Color(0.10f, 0.65f, 0.20f), // green
            new Color(0.95f, 0.80f, 0.10f), // yellow
            Color.white,
            Color.black,
        };
        var buttons = new Button[presets.Length];
        const float gap = 6f, swH = 30f, swY = 40f;
        float sw = (innerW - gap * (presets.Length - 1)) / presets.Length;
        for (int i = 0; i < presets.Length; i++)
        {
            buttons[i] = NewButton(rrt, "", i * (sw + gap), swY, sw, swH, presets[i], out _);
            buttons[i].name = "Swatch" + i;
        }
        ctrl.swatchButtons = buttons;
        ctrl.swatchColors = presets;
    }

    // A clickable uGUI button (background Image + optional centered label).
    static Button NewButton(RectTransform parent, string label, float x, float y, float w, float h,
                            Color bg, out Text labelText)
    {
        var go = new GameObject(label.Length > 0 ? label.Replace(" ", "") : "Button",
                                typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);

        var img = go.GetComponent<Image>();
        img.color = bg;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        labelText = null;
        if (label.Length > 0)
        {
            labelText = NewLabel(rt, label, 0f, 0f, w, h, TextAnchor.MiddleCenter);
            labelText.resizeTextForBestFit = true;   // shrink/grow to fit the button
            labelText.resizeTextMinSize = 8;
            labelText.resizeTextMaxSize = 26;
        }
        return btn;
    }

    // One labeled control row: name on the left, live value on the right, slider underneath.
    // 'index' stacks rows top-down inside the panel. Reused by M5.3/M5.4.
    static Slider BuildControlRow(RectTransform panel, string label, float min, float max,
                                  float value, int index, out Text valueText)
    {
        const float rowH = 80f, top = 14f, pad = 12f, innerW = 336f;
        var row = new GameObject(label.Replace(" ", "") + "Row", typeof(RectTransform));
        var rrt = row.GetComponent<RectTransform>();
        rrt.SetParent(panel, false);
        rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
        rrt.pivot = new Vector2(0f, 1f);
        rrt.anchoredPosition = new Vector2(pad, -(top + index * rowH));
        rrt.sizeDelta = new Vector2(innerW, rowH - 8f);

        NewLabel(rrt, label, 0f, 0f, 236f, 32f, TextAnchor.MiddleLeft);
        valueText = NewLabel(rrt, value.ToString("0.0"), innerW - 100f, 0f, 100f, 32f, TextAnchor.MiddleRight);
        return NewSlider(rrt, 0f, 40f, innerW, 26f, min, max, value);
    }

    static Text NewLabel(RectTransform parent, string text, float x, float y, float w, float h, TextAnchor anchor)
    {
        var go = new GameObject("Label", typeof(Text));
        var t = go.GetComponent<Text>();
        var rt = t.rectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 26;
        t.fontStyle = FontStyle.Bold;
        // Draw even when the rect is shorter than the font's line height — otherwise
        // Unity's Truncate mode hides the WHOLE line (the "invisible title" bug).
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = Color.white;
        t.alignment = anchor;
        t.text = text;
        return t;
    }

    // A fully wired uGUI slider built from scratch (background + fill + handle).
    static Slider NewSlider(RectTransform parent, float x, float y, float w, float h,
                            float min, float max, float value)
    {
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);

        var bg = NewImage("Background", rt, new Color(1f, 1f, 1f, 0.25f));
        Stretch(bg.rectTransform);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        var fart = (RectTransform)fillArea.transform;
        fart.SetParent(rt, false);
        Stretch(fart); fart.offsetMin = new Vector2(5f, 0f); fart.offsetMax = new Vector2(-5f, 0f);
        var fill = NewImage("Fill", fart, new Color(0.3f, 0.7f, 1f, 1f));
        fill.rectTransform.anchorMin = new Vector2(0f, 0f);
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

        var hsa = new GameObject("Handle Slide Area", typeof(RectTransform));
        var hsart = (RectTransform)hsa.transform;
        hsart.SetParent(rt, false);
        Stretch(hsart); hsart.offsetMin = new Vector2(5f, 0f); hsart.offsetMax = new Vector2(-5f, 0f);
        var handle = NewImage("Handle", hsart, Color.white);
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(16f, 0f);

        var s = go.GetComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min; s.maxValue = max; s.value = value;
        return s;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        var img = go.GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.color = color;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // Euler X is reported as 0..360; fold to a signed angle so a flat canvas reads 0, not 360.
    static float NormalizeTiltEuler(float x) => x > 180f ? x - 360f : x;

    // Compact labeled slider (~56px): "Label ..... value" on one line, slider under it.
    // Used by the Environment panel, which stacks many controls in limited height.
    static Slider BuildCompactRow(RectTransform panel, float baseY, int index, string label,
                                  float min, float max, float value, out Text valueText, string fmt = "0.0")
    {
        const float rowH = 56f, innerW = 336f, pad = 12f;
        float y = baseY + index * rowH;
        var lbl = NewLabel(panel, label, pad, y, 230f, 22f, TextAnchor.MiddleLeft);
        lbl.fontSize = 18;
        valueText = NewLabel(panel, value.ToString(fmt), pad + innerW - 90f, y, 90f, 22f, TextAnchor.MiddleRight);
        valueText.fontSize = 18;
        return NewSlider(panel, pad, y + 26f, innerW, 16f, min, max, value);
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
