using UnityEngine;
using UnityEngine.UI;

// M5 - Runtime control panel.
// This script is the single link between the on-screen UI (sliders/buttons) and the
// simulation (bucket motion + SPH fluid + canvas). References are auto-filled by the
// Tools menu; the sliders are built by the menu and wired here.
//
// Allowed by the project constraint: uGUI is a UI system, not a physics helper
// (no Collider / Rigidbody / Joint). All physics stays our own math in Bucket/SphFluid.
public class UIControlPanel : MonoBehaviour
{
    [Header("Simulation references (auto-filled by the Tools menu)")]
    public Bucket bucket;          // pendulum motion (rope length, angle, speed)
    public SphFluid bucketFluid;   // the paint inside the bucket (viscosity, hole, color, ...)
    public PaintCanvas canvas;     // the artwork surface (clear / save)

    [Header("M5.2 - Physics sliders (auto-filled by the Tools menu)")]
    public Slider ropeSlider;  public Text ropeValue;   // bucket.l
    public Slider angleSlider; public Text angleValue;  // bucket.thetaMax
    public Slider speedSlider; public Text speedValue;  // bucket.omega
    public Slider swingSlider; public Text swingValue;  // bucket.swingCount (0 = unlimited)

    [Header("M5.3 - Liquid sliders (auto-filled by the Tools menu)")]
    public Slider viscositySlider; public Text viscosityValue; // bucketFluid.viscosity
    public Slider holeSlider;      public Text holeValue;      // bucketFluid.holeDiameter
    public Slider splatSlider;     public Text splatValue;     // bucketFluid.splatRadius

    [Header("M5.4 - Color (auto-filled by the Tools menu)")]
    public Button[] swatchButtons;     // preset color buttons
    public Color[]  swatchColors;      // matching colors (parallel to swatchButtons)
    public Slider rSlider; public Text rValue;   // paintColor.r
    public Slider gSlider; public Text gValue;   // paintColor.g
    public Slider bSlider; public Text bValue;   // paintColor.b

    [Header("M5.4b - Action buttons (auto-filled by the Tools menu)")]
    public Button holeButton; public Text holeLabel; // toggles bucketFluid.holeOpen
    public Button clearButton;                        // calls canvas.Clear()

    [Header("M5.5 - Save / Reset (auto-filled by the Tools menu)")]
    public Button saveButton;   // canvas.SavePng()
    public Button resetButton;  // restore the authored default values

    [Header("S3 - Complexity demo (auto-filled by the Tools menu)")]
    public Button methodButton; public Text methodLabel;   // toggle Grid O(n) / Brute O(n^2)
    public Slider particleSlider; public Text particleValue;
    public Button applyButton;                              // respawn at the chosen count
    public Text statsReadout;                               // live "mode  X.XX ms"

    [Header("C1 - Environment & scene (auto-filled by the Tools menu)")]
    public Slider gravitySlider;    public Text gravityValue;    // fluid gravity magnitude
    public Slider bounceSlider;     public Text bounceValue;     // wall bounce (boundaryDamping)
    public Slider canvasSizeSlider; public Text canvasSizeValue; // canvas world size
    public Slider canvasTiltSlider; public Text canvasTiltValue; // canvas tilt angle (deg)
    public Slider airSlider;        public Text airValue;        // air resistance (0..1)
    public Slider humiditySlider;   public Text humidityValue;   // humidity (0..1) -> splat spread
    public Button motionButton;     public Text motionLabel;     // pendulum <-> circular
    public Button surfaceButton;    public Text surfaceLabel;    // canvas / wood / metal / paper

    [Header("M6.3 - Comparison (auto-filled by the Tools menu)")]
    public Text comparisonText;   // live "previous -> current" experiment table

    // Snapshot of the scene's authored values, captured at Start, used by Reset.
    float defL, defAngle, defOmega, defViscosity, defHole, defSplat;
    float defGravity, defBounce, defCanvasSize, defCanvasTilt;
    Color defColor; bool defHoleOpen, defCircular; int defSwing;

    // Euler X comes back as 0..360; fold into a signed angle so 0 stays 0 (not 360).
    static float NormalizeTilt(float x) => x > 180f ? x - 360f : x;

    // M6.3 - one experiment's inputs + results, for the comparison table.
    struct Snapshot
    {
        public bool valid;
        public float rope, angle, speed, gravity, viscosity, hole, canvasSize, time, coverage;
        public int trails, used, total;
        public bool circular;
        public Color colour;
    }
    Snapshot prevExp;        // captured whenever the user saves an experiment
    float comparisonTimer;

    void Start()
    {
        if (bucket != null)
        {
            Bind(ropeSlider,  ropeValue,  bucket.l,        v => bucket.l        = v);
            Bind(angleSlider, angleValue, bucket.thetaMax, v => bucket.thetaMax = v);
            Bind(speedSlider, speedValue, bucket.omega,    v => bucket.omega    = v);
            // Swing count: 0 = unlimited. Changing it restarts the swing from the beginning.
            Bind(swingSlider, swingValue, bucket.swingCount,
                 v => { bucket.swingCount = Mathf.RoundToInt(v); bucket.RestartSwing(); }, "0");
        }
        if (bucketFluid != null)
        {
            Bind(viscositySlider, viscosityValue, bucketFluid.viscosity,    v => bucketFluid.viscosity    = v);
            Bind(holeSlider,      holeValue,      bucketFluid.holeDiameter, v => bucketFluid.holeDiameter = v, "0.00");
            Bind(splatSlider,     splatValue,     bucketFluid.splatRadius,  v => bucketFluid.splatRadius  = v, "0.00");

            // RGB sliders write into paintColor's channels (keeping its alpha).
            Color c = bucketFluid.paintColor;
            Bind(rSlider, rValue, c.r, v => { var k = bucketFluid.paintColor; k.r = v; bucketFluid.paintColor = k; }, "0.00");
            Bind(gSlider, gValue, c.g, v => { var k = bucketFluid.paintColor; k.g = v; bucketFluid.paintColor = k; }, "0.00");
            Bind(bSlider, bValue, c.b, v => { var k = bucketFluid.paintColor; k.b = v; bucketFluid.paintColor = k; }, "0.00");

            // Preset swatches set the whole color and sync the RGB sliders.
            if (swatchButtons != null)
                for (int i = 0; i < swatchButtons.Length; i++)
                {
                    if (swatchButtons[i] == null) continue;
                    Color preset = (swatchColors != null && i < swatchColors.Length) ? swatchColors[i] : Color.white;
                    swatchButtons[i].onClick.AddListener(() => SetPaintColor(preset));
                }

            // Hole toggle: open/close the drain and reflect it on the button label.
            if (holeButton != null)
            {
                UpdateHoleLabel();
                holeButton.onClick.AddListener(() =>
                {
                    bucketFluid.holeOpen = !bucketFluid.holeOpen;
                    UpdateHoleLabel();
                });
            }
        }

        // Clear button wipes the canvas back to blank.
        if (clearButton != null && canvas != null)
            clearButton.onClick.AddListener(() => canvas.Clear());

        // C1 - environment & scene controls.
        if (bucketFluid != null)
        {
            Bind(gravitySlider, gravityValue, -bucketFluid.gravity.y,
                 v => bucketFluid.gravity = new Vector3(0f, -v, 0f), "0.00");
            Bind(bounceSlider, bounceValue, bucketFluid.boundaryDamping,
                 v => bucketFluid.boundaryDamping = v, "0.00");
        }
        if (canvas != null)
        {
            Bind(canvasSizeSlider, canvasSizeValue, canvas.transform.localScale.x,
                 v => canvas.transform.localScale = new Vector3(v, 1f, v));
            // Canvas tilt: rotate the whole canvas about its local X. The paint hit-test is
            // an analytic plane derived from the transform, so it follows the tilt for free.
            Bind(canvasTiltSlider, canvasTiltValue, NormalizeTilt(canvas.transform.localEulerAngles.x),
                 v => canvas.transform.localRotation = Quaternion.Euler(v, 0f, 0f));
        }
        // Air resistance: one 0..1 slider drives both the pendulum decay and the paint drag.
        Bind(airSlider, airValue, 0f, v =>
        {
            if (bucket != null) bucket.airDamping = v * 0.4f;
            if (bucketFluid != null) bucketFluid.airResistance = v * 3f;
        }, "0.00");
        // Humidity: a wetter surface makes each droplet spread wider (see SphFluid.humidity).
        Bind(humiditySlider, humidityValue, bucketFluid != null ? bucketFluid.humidity : 0f,
             v => { if (bucketFluid != null) bucketFluid.humidity = v; }, "0.00");

        if (motionButton != null && bucket != null)
        {
            UpdateMotionLabel();
            motionButton.onClick.AddListener(() =>
            {
                bucket.useCircularMotion = !bucket.useCircularMotion;
                UpdateMotionLabel();
            });
        }

        // Surface type: a cycling button (canvas -> wood -> metal -> paper).
        ApplySurface(surfaceIndex, false);   // sync label to the authored default without wiping art
        if (surfaceButton != null)
            surfaceButton.onClick.AddListener(() => ApplySurface((surfaceIndex + 1) % SurfaceNames.Length, true));

        // Remember the authored defaults so Reset can restore them.
        if (bucket != null)
        {
            defL = bucket.l; defAngle = bucket.thetaMax; defOmega = bucket.omega;
            defCircular = bucket.useCircularMotion; defSwing = bucket.swingCount;
        }
        if (bucketFluid != null)
        {
            defViscosity = bucketFluid.viscosity; defHole = bucketFluid.holeDiameter;
            defSplat = bucketFluid.splatRadius; defColor = bucketFluid.paintColor;
            defHoleOpen = bucketFluid.holeOpen;
            defGravity = -bucketFluid.gravity.y; defBounce = bucketFluid.boundaryDamping;
        }
        if (canvas != null)
        {
            defCanvasSize = canvas.transform.localScale.x;
            defCanvasTilt = NormalizeTilt(canvas.transform.localEulerAngles.x);
        }

        if (saveButton != null && canvas != null)
            saveButton.onClick.AddListener(SaveExperiment);
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetDefaults);

        // S3 - complexity demo controls
        if (bucketFluid != null)
        {
            UpdateMethodLabel();
            if (methodButton != null)
                methodButton.onClick.AddListener(() => { bucketFluid.ToggleNeighborMethod(); UpdateMethodLabel(); });

            if (particleSlider != null)
            {
                particleSlider.wholeNumbers = true;
                particleSlider.SetValueWithoutNotify(bucketFluid.ParticleCount);
                if (particleValue != null) particleValue.text = bucketFluid.ParticleCount.ToString();
                particleSlider.onValueChanged.AddListener(v =>
                { if (particleValue != null) particleValue.text = Mathf.RoundToInt(v).ToString(); });
            }
            if (applyButton != null && particleSlider != null)
                applyButton.onClick.AddListener(() => bucketFluid.Respawn(Mathf.RoundToInt(particleSlider.value)));
        }
    }

    void Update()
    {
        if (statsReadout != null && bucketFluid != null)
            statsReadout.text = (bucketFluid.IsBruteForce ? "Brute O(n^2)" : "Grid O(n)")
                              + "   " + bucketFluid.NeighborMs.ToString("0.00") + " ms";

        // M6.3 - refresh the "previous -> current" table 4x per second.
        if (comparisonText != null)
        {
            comparisonTimer += Time.deltaTime;
            if (comparisonTimer >= 0.25f)
            {
                comparisonTimer = 0f;
                comparisonText.text = BuildComparison();
            }
        }
    }

    // M6.3 - capture the current experiment (inputs + measured results).
    Snapshot Capture()
    {
        var s = new Snapshot { valid = true };
        if (bucket != null)
        {
            s.rope = bucket.l; s.angle = bucket.thetaMax; s.speed = bucket.omega;
            s.circular = bucket.useCircularMotion;
        }
        if (bucketFluid != null)
        {
            s.gravity = -bucketFluid.gravity.y; s.viscosity = bucketFluid.viscosity;
            s.hole = bucketFluid.holeDiameter; s.colour = bucketFluid.paintColor;
            s.time = bucketFluid.MotionTime; s.used = bucketFluid.PaintUsed;
            s.total = bucketFluid.ParticleCount;
        }
        if (canvas != null)
        {
            s.canvasSize = canvas.transform.localScale.x;
            s.trails = canvas.StrokeCount; s.coverage = canvas.CoveragePercent;
        }
        return s;
    }

    string BuildComparison()
    {
        if (!prevExp.valid)
            return "No saved experiment yet.\nPress 'Save PNG' to record one,\nthen change values and run again.";

        Snapshot p = prevExp, c = Capture();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("previous -> current");
        sb.AppendLine($"Rope       {p.rope:0.#} -> {c.rope:0.#}");
        sb.AppendLine($"Angle      {p.angle:0.#} -> {c.angle:0.#}");
        sb.AppendLine($"Speed      {p.speed:0.#} -> {c.speed:0.#}");
        sb.AppendLine($"Motion     {(p.circular ? "Circular" : "Pendulum")} -> {(c.circular ? "Circular" : "Pendulum")}");
        sb.AppendLine($"Gravity    {p.gravity:0.##} -> {c.gravity:0.##}");
        sb.AppendLine($"Viscosity  {p.viscosity:0.#} -> {c.viscosity:0.#}");
        sb.AppendLine($"Hole       {p.hole:0.##} -> {c.hole:0.##}");
        sb.AppendLine($"Colour     #{ColorUtility.ToHtmlStringRGB(p.colour)} -> #{ColorUtility.ToHtmlStringRGB(c.colour)}");
        sb.AppendLine($"Canvas     {p.canvasSize:0.#} -> {c.canvasSize:0.#}");
        sb.AppendLine("--------- results ---------");
        sb.AppendLine($"Time       {p.time:0.#}s -> {c.time:0.#}s");
        sb.AppendLine($"Trails     {p.trails} -> {c.trails}");
        sb.AppendLine($"Coverage   {p.coverage:0.#}% -> {c.coverage:0.#}%");
        sb.Append($"Paint      {p.used}/{p.total} -> {c.used}/{c.total}");
        return sb.ToString();
    }

    void UpdateMethodLabel()
    {
        if (methodLabel != null && bucketFluid != null)
            methodLabel.text = bucketFluid.IsBruteForce ? "Mode: Brute O(n^2)" : "Mode: Grid O(n)";
    }

    // Restore every control to the scene's authored default. Setting a slider's value
    // fires its listener, which pushes the value back into the simulation.
    public void ResetDefaults()
    {
        if (bucket != null)
        {
            if (ropeSlider  != null) ropeSlider.value  = defL;
            if (angleSlider != null) angleSlider.value = defAngle;
            if (speedSlider != null) speedSlider.value = defOmega;
            if (swingSlider != null) swingSlider.value = defSwing;
        }
        if (bucketFluid != null)
        {
            if (viscositySlider != null) viscositySlider.value = defViscosity;
            if (holeSlider      != null) holeSlider.value      = defHole;
            if (splatSlider     != null) splatSlider.value     = defSplat;
            if (gravitySlider   != null) gravitySlider.value   = defGravity;
            if (bounceSlider    != null) bounceSlider.value    = defBounce;
            SetPaintColor(defColor);
            bucketFluid.holeOpen = defHoleOpen;
            UpdateHoleLabel();
        }
        if (canvasSizeSlider != null) canvasSizeSlider.value = defCanvasSize;
        if (canvasTiltSlider != null) canvasTiltSlider.value = defCanvasTilt;
        if (airSlider        != null) airSlider.value        = 0f;   // authored default = no air resistance
        if (humiditySlider   != null) humiditySlider.value   = 0f;   // authored default = dry (no extra spread)
        if (bucket != null) { bucket.useCircularMotion = defCircular; UpdateMotionLabel(); }
        ApplySurface(0, true);   // back to the default Canvas surface (fresh)
    }

    void UpdateMotionLabel()
    {
        if (motionLabel != null && bucket != null)
            motionLabel.text = bucket.useCircularMotion ? "Motion: Circular" : "Motion: Pendulum";
    }

    // --- Surface type presets (canvas / wood / metal / paper) ---
    // Each surface differs in base colour and absorbency (edge softness + paint spread).
    static readonly string[] SurfaceNames = { "Canvas", "Wood", "Metal", "Paper" };
    static readonly Color[]  SurfaceColors =
    {
        new Color(0.95f, 0.95f, 0.92f, 1f),  // Canvas - warm off-white
        new Color(0.80f, 0.62f, 0.42f, 1f),  // Wood   - light brown
        new Color(0.72f, 0.74f, 0.78f, 1f),  // Metal  - cool grey
        new Color(0.98f, 0.98f, 0.98f, 1f),  // Paper  - white
    };
    static readonly float[] SurfaceSoftness = { 0.50f, 0.55f, 0.15f, 0.80f }; // metal hard, paper soft
    static readonly float[] SurfaceSpread   = { 1.00f, 1.05f, 0.80f, 1.25f }; // metal beads, paper bleeds
    int surfaceIndex = 0;

    // Apply a surface preset. When clearArt is true the canvas is repainted with the new base
    // colour (a fresh surface); when false we only sync the label/values (used at startup/Reset).
    void ApplySurface(int idx, bool clearArt)
    {
        surfaceIndex = Mathf.Clamp(idx, 0, SurfaceNames.Length - 1);
        if (canvas != null)
        {
            canvas.surfaceColor = SurfaceColors[surfaceIndex];
            canvas.edgeSoftness = SurfaceSoftness[surfaceIndex];
            if (clearArt) canvas.Clear();   // repaint the background = fresh surface
        }
        if (bucketFluid != null) bucketFluid.surfaceSpread = SurfaceSpread[surfaceIndex];
        if (surfaceLabel != null) surfaceLabel.text = "Surface: " + SurfaceNames[surfaceIndex];
    }

    // M6.2 - save the painting AND a text report of the whole experiment (PDF output 7):
    // every input plus the measured results, written next to the PNG with the same name.
    public void SaveExperiment()
    {
        if (canvas == null) return;
        string imgPath = canvas.SavePng();
        if (string.IsNullOrEmpty(imgPath)) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Swinging Paint Bucket - Experiment Report");
        sb.AppendLine("Saved:  " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Image:  " + System.IO.Path.GetFileName(imgPath));
        sb.AppendLine();
        sb.AppendLine("[Inputs]");
        if (bucket != null)
        {
            sb.AppendLine($"Rope length:        {bucket.l:0.##}");
            sb.AppendLine($"Release angle:      {bucket.thetaMax:0.##} deg");
            sb.AppendLine($"Speed (omega):      {bucket.omega:0.##}");
            sb.AppendLine($"Motion mode:        {(bucket.useCircularMotion ? "Circular" : "Pendulum")}");
            sb.AppendLine($"Swing count:        {(bucket.swingCount > 0 ? bucket.swingCount.ToString() : "unlimited")}");
        }
        if (bucketFluid != null)
        {
            sb.AppendLine($"Gravity:            {-bucketFluid.gravity.y:0.##}");
            sb.AppendLine($"Wall bounce:        {bucketFluid.boundaryDamping:0.##}");
            sb.AppendLine($"Air resistance:     {(airSlider != null ? airSlider.value : 0f):0.##}");
            sb.AppendLine($"Humidity:           {bucketFluid.humidity:0.##}");
            sb.AppendLine($"Viscosity:          {bucketFluid.viscosity:0.##}");
            sb.AppendLine($"Hole diameter:      {bucketFluid.holeDiameter:0.##} ({(bucketFluid.holeOpen ? "open" : "closed")})");
            sb.AppendLine($"Splat width:        {bucketFluid.splatRadius:0.##}");
            Color c = bucketFluid.paintColor;
            sb.AppendLine($"Paint colour (RGB): {c.r:0.##}, {c.g:0.##}, {c.b:0.##}");
            sb.AppendLine($"Paint amount:       {bucketFluid.ParticleCount} particles");
        }
        sb.AppendLine($"Canvas size:        {canvas.transform.localScale.x:0.##}");
        sb.AppendLine($"Canvas tilt:        {NormalizeTilt(canvas.transform.localEulerAngles.x):0.##} deg");
        sb.AppendLine($"Surface type:       {SurfaceNames[surfaceIndex]}");
        sb.AppendLine();
        sb.AppendLine("[Results]");
        if (bucketFluid != null)
        {
            sb.AppendLine($"Motion time:        {bucketFluid.MotionTime:0.#} s");
            sb.AppendLine($"Paint used:         {bucketFluid.PaintUsed} / {bucketFluid.ParticleCount}");
        }
        sb.AppendLine($"Number of trails:   {canvas.StrokeCount}");
        sb.AppendLine($"Colour spread area: {canvas.CoveragePercent:0.#} % of the canvas");

        string txtPath = System.IO.Path.ChangeExtension(imgPath, ".txt");
        System.IO.File.WriteAllText(txtPath, sb.ToString());
        Debug.Log("[Experiment] Report saved to: " + txtPath);

        // M6.3: a saved experiment becomes the "previous" side of the comparison table.
        prevExp = Capture();
    }

    void UpdateHoleLabel()
    {
        if (holeLabel != null && bucketFluid != null)
            holeLabel.text = bucketFluid.holeOpen ? "Hole: Open" : "Hole: Closed";
    }

    // Apply a full color to the paint and reflect it on the RGB sliders (without re-firing them).
    public void SetPaintColor(Color c)
    {
        if (bucketFluid == null) return;
        c.a = bucketFluid.paintColor.a; // preserve alpha
        bucketFluid.paintColor = c;
        SyncColor(rSlider, rValue, c.r);
        SyncColor(gSlider, gValue, c.g);
        SyncColor(bSlider, bValue, c.b);
    }

    void SyncColor(Slider s, Text label, float v)
    {
        if (s != null) s.SetValueWithoutNotify(v);
        if (label != null) label.text = v.ToString("0.00");
    }

    // Set the slider to the simulation's current value and route future changes back to it,
    // updating the numeric read-out next to the slider.
    void Bind(Slider s, Text label, float current, System.Action<float> apply, string fmt = "0.0")
    {
        if (s == null) return;
        s.SetValueWithoutNotify(current);
        if (label != null) label.text = current.ToString(fmt);
        s.onValueChanged.AddListener(v =>
        {
            apply(v);
            if (label != null) label.text = v.ToString(fmt);
        });
    }
}
