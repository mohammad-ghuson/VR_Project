using UnityEngine;

public class Bucket : MonoBehaviour
{
    Vector3 pos , pos0;
    public float l = 1;
    public float thetaMax = 60;
    public float omega = 1;
    float time = 0;
    public float dt = 0.01f;

    public bool useCircularMotion = false;

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

        if (useCircularMotion)
        {
            float theta = omega * time;

            pos.x = l * Mathf.Cos(theta);
            pos.z = l * Mathf.Sin(theta);
        }
        else
        {
            float thetaMaxRad = toRad(thetaMax);

            float thetaRad = thetaMaxRad * Mathf.Cos(omega * time);

            pos.x = l * Mathf.Sin(thetaRad);
            pos.y = -l * Mathf.Cos(thetaRad);
        }

        this.transform.position = pos + pos0;
    }
}
