using UnityEngine;

// T3: gently oscillates an object (the tank) so the SPH liquid inside sloshes.
// Pure math + transform; no physics engine. Runs in Update, before SphFluid (LateUpdate),
// so the fluid reads the moved walls and the wall velocity drives the sloshing.
public class Shaker : MonoBehaviour
{
    [Header("Rock (tilt back and forth)")]
    public bool rock = true;
    public float rockAmplitude = 15f;      // degrees
    public float rockSpeed = 2f;           // radians / second
    public Vector3 rockAxis = Vector3.forward;

    [Header("Slide (move side to side)")]
    public bool slide = false;
    public float slideAmplitude = 0.3f;    // world units
    public float slideSpeed = 2f;
    public Vector3 slideAxis = Vector3.right;

    Vector3 basePos;
    Quaternion baseRot;
    float t;

    void Start()
    {
        basePos = transform.position;
        baseRot = transform.rotation;
    }

    void Update()
    {
        t += Time.deltaTime;

        Quaternion r = baseRot;
        if (rock)
        {
            float a = rockAmplitude * Mathf.Sin(rockSpeed * t);
            r = baseRot * Quaternion.AngleAxis(a, rockAxis.normalized);
        }
        transform.rotation = r;

        Vector3 p = basePos;
        if (slide)
            p += slideAxis.normalized * (slideAmplitude * Mathf.Sin(slideSpeed * t));
        transform.position = p;
    }
}
