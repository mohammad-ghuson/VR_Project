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
    public Button motionButton;     public Text motionLabel;     // pendulum <-> circular

    [Header("M6.3 - Comparison (auto-filled by the Tools menu)")]
    public Text comparisonText;   // live "previous -> current" experiment table

    // Snapshot of the scene's authored values, captured at Start, used by Reset.
    float defL, defAngle, defOmega, defViscosity, defHole, defSplat;
    float defGravity, defBounce, defCanvasSize;
    Color defColor; bool defHoleOpen, defCircular;

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
            Bind(canvasSizeSlider, canvasSizeValue, canvas.transform.localScale.x,
                 v => canvas.transform.localScale = new Vector3(v, 1f, v));
        if (motionButton != null && bucket != null)
        {
            UpdateMotionLabel();
            motionButton.onClick.AddListener(() =>
            {
                bucket.useCircularMotion = !bucket.useCircularMotion;
                UpdateMotionLabel();
            });
        }

        // Remember the authored defaults so Reset can restore them.
        if (bucket != null)
        {
            defL = bucket.l; defAngle = bucket.thetaMax; defOmega = bucket.omega;
            defCircular = bucket.useCircularMotion;
        }
        if (bucketFluid != null)
        {
            defViscosity = bucketFluid.viscosity; defHole = bucketFluid.holeDiameter;
            defSplat = bucketFluid.splatRadius; defColor = bucketFluid.paintColor;
            defHoleOpen = bucketFluid.holeOpen;
            defGravity = -bucketFluid.gravity.y; defBounce = bucketFluid.boundaryDamping;
        }
        if (canvas != null) defCanvasSize = canvas.transform.localScale.x;

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
        if (bucket != null) { bucket.useCircularMotion = defCircular; UpdateMotionLabel(); }
    }

    void UpdateMotionLabel()
    {
        if (motionLabel != null && bucket != null)
            motionLabel.text = bucket.useCircularMotion ? "Motion: Circular" : "Motion: Pendulum";
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
        }
        if (bucketFluid != null)
        {
            sb.AppendLine($"Gravity:            {-bucketFluid.gravity.y:0.##}");
            sb.AppendLine($"Wall bounce:        {bucketFluid.boundaryDamping:0.##}");
            sb.AppendLine($"Viscosity:          {bucketFluid.viscosity:0.##}");
            sb.AppendLine($"Hole diameter:      {bucketFluid.holeDiameter:0.##} ({(bucketFluid.holeOpen ? "open" : "closed")})");
            sb.AppendLine($"Splat width:        {bucketFluid.splatRadius:0.##}");
            Color c = bucketFluid.paintColor;
            sb.AppendLine($"Paint colour (RGB): {c.r:0.##}, {c.g:0.##}, {c.b:0.##}");
            sb.AppendLine($"Paint amount:       {bucketFluid.ParticleCount} particles");
        }
        sb.AppendLine($"Canvas size:        {canvas.transform.localScale.x:0.##}");
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
