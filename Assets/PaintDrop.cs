using UnityEngine;

public class PaintDrop : MonoBehaviour
{
    public Vector3 v = new Vector3();
    public Vector3 a = new Vector3();
    Vector3 scaleEnd = new Vector3(2, 0.1f, 2);
    Vector3 pos;
    public float g = 9.8f;
    public float dt = 0.01f;
    public float yGround = 0;


    void Start()
    {
        pos = transform.position;
    }

    void Update()
    {
        if (pos.y > yGround + 0.1f)
        {
            a = new Vector3(0, -g, 0);
            v += a * dt;
            pos += v * dt;
            transform.position = pos;
        }
        else
        {
            pos.y = yGround + 0.1f;
            transform.localScale = scaleEnd;
        }
    }
}
