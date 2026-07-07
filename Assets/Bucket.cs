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

        if (useCircularMotion)
        {
            float theta = omega * time;

            // Swing budget: after N full revolutions, stop orbiting (hold at theta = 0).
            if (swingCount > 0 && omega > 0f && time >= swingCount * 2f * Mathf.PI / omega)
                theta = 0f;

            pos.x = l * decay * Mathf.Cos(theta);
            pos.z = l * decay * Mathf.Sin(theta);
        }
        else
        {
            float thetaMaxRad = toRad(thetaMax);

            float thetaRad = thetaMaxRad * decay * Mathf.Cos(omega * time);

            // Swing budget: after N full swings, settle at the bottom (vertical rest). The stop
            // time is a bottom-crossing, so the angle is already ~0 there — no visual snap.
            if (swingCount > 0 && omega > 0f && time >= (2 * swingCount + 0.5f) * Mathf.PI / omega)
                thetaRad = 0f;

            pos.x = l * Mathf.Sin(thetaRad);
            pos.y = -l * Mathf.Cos(thetaRad);
        }

        this.transform.position = pos + pos0;
    }
}
