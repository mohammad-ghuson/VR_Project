using UnityEngine;

// Phase 1 - Step 4. Tilts the bucket while it swings, as if it hung from a rope,
// WITHOUT modifying Bucket.cs. Pure math + transform only (no physics).
// Runs in LateUpdate so it reads the position Bucket.cs set earlier this frame.
public class BucketTilt : MonoBehaviour
{
    [Tooltip("Exaggerate (>1) or dampen (<1) the tilt amount.")]
    public float tiltMultiplier = 1f;

    [Tooltip("Hard limit on tilt angle (degrees) to keep liquid clipping reasonable.")]
    public float maxTilt = 45f;

    Bucket bucket;            // read the real (possibly lifted) pivot from here
    Quaternion baseRotation;  // the bucket's upright orientation

    void Start()
    {
        baseRotation = transform.rotation;
        bucket = GetComponent<Bucket>();
    }

    void LateUpdate()
    {
        // Use the bucket's actual suspension point (includes Pivot Lift), so the tilt aligns the
        // bucket with the real rope direction and its rim stays attached to the rope end.
        Vector3 pivot = bucket != null ? bucket.PivotWorld : transform.position;
        Vector3 toPivot = pivot - transform.position;
        if (toPivot.sqrMagnitude < 1e-8f) { transform.rotation = baseRotation; return; }

        // The bucket's up should point along the rope (from bucket toward the pivot).
        Vector3 ropeUp = toPivot.normalized;
        Vector3 axis = Vector3.Cross(Vector3.up, ropeUp);
        if (axis.sqrMagnitude < 1e-8f) { transform.rotation = baseRotation; return; }
        axis.Normalize();

        float angle = Mathf.Min(Vector3.Angle(Vector3.up, ropeUp) * tiltMultiplier, maxTilt);
        transform.rotation = Quaternion.AngleAxis(angle, axis) * baseRotation;
    }
}
