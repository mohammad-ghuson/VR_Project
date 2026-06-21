using UnityEngine;

// Phase 1 - Step 2 ONLY.
// Keeps the liquid surface horizontal in world space, no matter how the parent
// bucket tilts or rotates. Visual only: no physics and no sloshing yet (Step 5).
// [ExecuteAlways] lets you test it in the Scene view without entering Play mode.
// High execution order so it runs AFTER BucketTilt, keeping the surface level
// even on the same frame the bucket tilts (no one-frame wobble).
[ExecuteAlways]
[DefaultExecutionOrder(100)]
public class LiquidController : MonoBehaviour
{
    void LateUpdate()
    {
        // World-up orientation: the surface never pitches or rolls with the bucket.
        // Unity's Quaternion != uses an approximate compare, so we only write when needed.
        if (transform.rotation != Quaternion.identity)
            transform.rotation = Quaternion.identity;
    }
}
