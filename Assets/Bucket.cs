using UnityEngine;

public class Bucket : MonoBehaviour
{
    Vector3 pos , pos0;
    public float l = 1;
    public float thetaMax = 60;
    public float omega = 1;
    float time = 0;
    public float dt = 0.01f;

    // Air resistance: the swing amplitude decays as exp(-airDamping * time), so the pendulum
    // gradually settles (0 = no damping = unchanged behaviour). Driven by the UI slider.
    public float airDamping = 0f;

    // Swing budget: after this many full swings the bucket settles at the vertical rest
    // position. 0 = unlimited = unchanged behaviour. Driven by the UI slider.
    public int swingCount = 0;

    public bool useCircularMotion = false;

    // Restart the motion from the beginning (used when the swing count changes or on Reset).
    public void RestartSwing() { time = 0f; }

    // Raise the suspension (pivot) point above the bucket's authored position, so you can use a
    // longer rope / larger attach offset without the bucket sinking below the canvas.
    public float pivotLift = 0f;

    // The fixed suspension (pivot) point = the authored position, raised by pivotLift.
    // Before play it is based on the current transform position. Used by the visible rope.
    public Vector3 PivotWorld => (Application.isPlaying ? pos0 : transform.position) + Vector3.up * pivotLift;

    // The point on the bucket the rope attaches to (its top rim centre), in the bucket's LOCAL
    // space. ProceduralBucket sets it to (0, height/2, 0). The rope draws to this real point on the
    // bucket, so it stays attached to the rim throughout the swing.
    public Vector3 ropeAttachLocal = new Vector3(0f, 0.45f, 0f);
    public Vector3 RopeAttachWorld => transform.TransformPoint(ropeAttachLocal);

    float toRad(float d)
    {
        return (d / 180) * (Mathf.PI);
    }

    void Start()
    {
        pos0 = transform.position;
    }

    //void Update()
    //{

    //  time += dt;

    //float thetaMaxRad = toRad(thetaMax);

    //float thetaRad = thetaMaxRad * Mathf.Cos(omega * time);
    //pos.x = l * Mathf.Sin(thetaRad);
    //pos.y = -l * Mathf.Cos(thetaRad);
    //this.transform.position = pos + pos0;

    //}

    void Update()
    {
        time += dt;

        // Air resistance damps the amplitude over time (1 = none).
        float decay = airDamping > 0f ? Mathf.Exp(-airDamping * time) : 1f;

        // Suspension point = authored position raised by pivotLift.
        Vector3 pivot = pos0 + Vector3.up * pivotLift;

        if (useCircularMotion)
        {
            float theta = omega * time;

            // Swing budget: after N full revolutions, stop orbiting (hold at theta = 0).
            if (swingCount > 0 && omega > 0f && time >= swingCount * 2f * Mathf.PI / omega)
                theta = 0f;

            Vector3 u = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta)); // unit horizontal
            pos = pivot + l * decay * u;                  // bucket orbits at radius l around the pivot
        }
        else
        {
            float thetaMaxRad = toRad(thetaMax);

            float thetaRad = thetaMaxRad * decay * Mathf.Cos(omega * time);

            // Swing budget: after N full swings, settle at the bottom (vertical rest). The stop
            // time is a bottom-crossing, so the angle is already ~0 there — no visual snap.
            if (swingCount > 0 && omega > 0f && time >= (2 * swingCount + 0.5f) * Mathf.PI / omega)
                thetaRad = 0f;

            // Unit direction from the pivot along the rope (straight down at angle 0).
            Vector3 radial = new Vector3(Mathf.Sin(thetaRad), -Mathf.Cos(thetaRad), 0f);
            pos = pivot + l * radial;                    // bucket centre swings at radius l
        }

        this.transform.position = pos;
    }
}
