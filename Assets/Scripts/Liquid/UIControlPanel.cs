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

    // Snapshot of the scene's authored values, captured at Start, used by Reset.
    float defL, defAngle, defOmega, defViscosity, defHole, defSplat;
    Color defColor; bool defHoleOpen;

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

        // Remember the authored defaults so Reset can restore them.
        if (bucket != null) { defL = bucket.l; defAngle = bucket.thetaMax; defOmega = bucket.omega; }
        if (bucketFluid != null)
        {
            defViscosity = bucketFluid.viscosity; defHole = bucketFluid.holeDiameter;
            defSplat = bucketFluid.splatRadius; defColor = bucketFluid.paintColor;
            defHoleOpen = bucketFluid.holeOpen;
        }

        if (saveButton != null && canvas != null)
            saveButton.onClick.AddListener(() => canvas.SavePng());
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
            SetPaintColor(defColor);
            bucketFluid.holeOpen = defHoleOpen;
            UpdateHoleLabel();
        }
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
