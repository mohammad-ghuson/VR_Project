using UnityEngine;

// A visible rope drawn as a thin cylinder from the fixed suspension (pivot) point straight
// toward the swinging bucket. Pure rendering + hand-written placement — no physics engine.
//
// The pendulum keeps the bucket at a CONSTANT distance (the rope length) from the pivot, so we
// draw the rope ALONG that pivot->bucket line and stop a little short of the bucket centre to
// meet the rim. Result: the rope keeps a constant length and never tilts oddly — i.e. a RIGID,
// inextensible rope, which matches the current pendulum. (Elastic rope TYPE comes later with the
// dynamic-pendulum upgrade.)
// Runs LATE (after BucketTilt, which tilts the bucket in LateUpdate) so the rope reads the bucket's
// FINAL rotation each frame. Without this, on the first frame the rope could run before the tilt and
// draw to the apex computed on the not-yet-tilted bucket — a one-frame "rope off the arc" glitch.
[ExecuteAlways]
[DefaultExecutionOrder(100)]
public class Rope : MonoBehaviour
{
    public Bucket bucket;              // the pendulum this rope hangs from
    public Transform anchor;           // optional visible suspension point, pinned at the pivot
    public Transform attachPoint;      // if set, the rope's lower end pins to this real transform
                                       // (the bail apex) — correct from the very first rendered frame
    public float thickness = 0.05f;    // rope diameter (world units)
    public float anchorScale = 0.12f;  // suspension-ball diameter (driven here so it always applies)

    void LateUpdate()
    {
        if (bucket == null) return;

        Vector3 top    = bucket.PivotWorld;              // fixed suspension point
        if (anchor != null)
        {
            anchor.position   = top;                     // pin the visible anchor to the pivot
            anchor.localScale = Vector3.one * anchorScale; // enforce a clean, consistent ball size
        }

        // Where the rope's lower end sits. Priority:
        //  1) an explicit apex transform, if wired;
        //  2) computed live from the bail handle (apex = rim height + handle rise) — correct from the
        //     very first rendered frame, no serialized value that could lag by a frame;
        //  3) the bucket's own rope attach point (no handle case).
        Vector3 bottom;
        if (attachPoint != null)
        {
            bottom = attachPoint.position;
        }
        else
        {
            var handle = bucket.GetComponentInChildren<BucketHandle>();
            if (handle != null && handle.isActiveAndEnabled)
            {
                var pb = bucket.GetComponent<ProceduralBucket>();
                float apexY = (pb != null ? pb.height * 0.5f : 0f) + handle.rise;
                bottom = bucket.transform.TransformPoint(new Vector3(0f, apexY, 0f));
            }
            else
            {
                bottom = bucket.RopeAttachWorld;
            }
        }
        Vector3 seg = bottom - top;
        float len = seg.magnitude;                       // = rope length
        if (len < 1e-4f) return;

        transform.position   = (top + bottom) * 0.5f;    // midpoint
        transform.up         = seg / len;                 // cylinder's local +Y follows the rope
        // Unity's built-in cylinder mesh is 2 units tall (y: -1..1), diameter 1 => scale maps directly.
        transform.localScale = new Vector3(thickness, len * 0.5f, thickness);
    }
}
