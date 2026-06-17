using UnityEngine;

public class SpScr : MonoBehaviour
{
    Vector3 pos;
    public Vector3 v = new Vector3();
    void Start()
    {
        pos = this.transform.position;
    }

    void Update()
    {
        pos += v;
        this.transform.position = pos;
    }
}
