using System.Collections.Generic;
using UnityEngine;

// Phase 1 (SPH) - Steps A-E.
// CPU SPH from scratch: gravity, neighbor search (spatial hash), density/pressure
// (Poly6 + Spiky), viscosity, and an analytic container that FOLLOWS the swinging,
// tilting bucket so the moving walls slosh the fluid. Pure C# math; no physics
// engine, no colliders, no ready-made packages. The particle mesh is built by us.
// Runs in LateUpdate after Bucket.cs (Update) and BucketTilt (LateUpdate, order 0).
[DefaultExecutionOrder(60)]
public class SphFluid : MonoBehaviour
{
    [Header("References")]
    public Transform bucket;          // used to size the container from its bounds
    public Material particleMaterial; // reuse LiquidPaint; auto-created if left empty

    [Header("Particles")]
    public int particleCount = 250;
    public float particleRadius = 0.05f;     // collision radius (world units)
    public float particleVisualScale = 1.0f; // visual size multiplier

    [Header("Container (auto-filled from bucket bounds at Start)")]
    public float innerRadiusFactor = 0.85f;
    public bool followBucket = true;          // Step E: container tracks the moving/tilting bucket
    public Vector3 containerCenter;
    public float containerRadius;
    public float containerBottomY;
    public float containerTopY;

    [Header("Simulation")]
    public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    public float timeStep = 0.005f;
    public float boundaryDamping = 0.4f;     // 0 = no bounce, 1 = full bounce
    [Range(1, 8)] public int maxSubSteps = 4;

    [Header("Neighbor search (Step B)")]
    public float smoothingRadius = 0.2f;     // h: neighbor radius (cell size of the grid)
    public bool logNeighborStats = false;    // turn on to verify the grid in the Console

    [Header("SPH forces (Step C)")]
    public float particleMass = 1f;
    public float restDensity = 1000f;        // rho0; auto-estimated at Start if enabled
    public bool autoRestDensity = true;
    public float stiffness = 3f;             // k: pressure response (raise = more incompressible)
    public float viscosity = 5f;             // mu: low = watery, high = thick paint (Step D)
    public float maxSpeed = 10f;             // safety clamp to prevent blow-ups

    [Header("Outflow (M3)")]
    public bool holeOpen = false;            // open the bottom hole so paint can drain
    public float holeDiameter = 0.2f;        // world-units diameter of the bottom hole
    public float despawnBelowY = -10f;       // escaped paint falling below this is removed (no canvas yet)

    [Header("Display (Step F)")]
    public bool showStats = true;            // on-screen FPS + particle count

    Vector3[] positions;
    Vector3[] velocities;
    float[] density;
    float[] pressure;
    Matrix4x4[] matrices;                     // per-particle transforms for instanced draw
    bool[] escaped;                           // true once a particle drained out the hole
    bool[] dead;                              // true once below the despawn plane (frozen + hidden)
    float particleDiameter;
    float fpsSmooth;
    float accumulator;

    // Flow measurement (M3.3)
    int drainedCount;          // total particles that have drained out the hole
    int lastDrained;
    int inBucketCount;         // particles still inside the bucket
    float flowRateSmooth;      // drained particles per second (smoothed)

    // Moving-container state (Step E). The cylinder follows the bucket each frame.
    Mesh particleMesh;
    bool spawned;
    float containerHalfHeight;
    Vector3 localContainerCenter;   // container center expressed in bucket-local space
    Vector3 worldContainerCenter;   // current world center of the cylinder
    Vector3 worldUp = Vector3.up;   // current cylinder axis (bucket's up)
    Vector3 containerLinVel;        // wall linear velocity (world)
    Vector3 containerAngVel;        // wall angular velocity (world, rad/s)
    Vector3 prevContainerCenter;
    Quaternion prevBucketRot = Quaternion.identity;

    // Spatial hash: maps a grid cell -> list of particle indices inside it.
    readonly Dictionary<Vector3Int, List<int>> grid = new Dictionary<Vector3Int, List<int>>();
    readonly Stack<List<int>> listPool = new Stack<List<int>>();
    readonly List<int> neighborScratch = new List<int>(64);
    int frameCounter;

    void Start()
    {
        if (!ComputeContainerFromBucket()) { enabled = false; return; }
        EnsureMaterial();
        particleMesh = BuildSphereMesh(8, 6, 0.5f); // radius 0.5 => diameter 1
        // Particles are spawned on the first LateUpdate, AFTER Bucket.cs has moved the
        // bucket on frame 1, so they appear at the bucket's real runtime position.
    }

    void LateUpdate()
    {
        UpdateContainerKinematics(Time.deltaTime);

        if (!spawned)
        {
            SpawnParticles();
            if (autoRestDensity)
            {
                BuildGrid();
                restDensity = AverageDensity();
                Debug.Log($"[SPH][StepC] auto restDensity = {restDensity:F1}");
            }
            spawned = true;
            RenderParticles();
            return; // skip simulating on the spawn frame
        }

        // Fixed-step integration for stability, capped to avoid the spiral of death.
        accumulator += Time.deltaTime;
        int steps = 0;
        while (accumulator >= timeStep && steps < maxSubSteps)
        {
            Step(timeStep);
            accumulator -= timeStep;
            steps++;
        }
        if (steps == maxSubSteps) accumulator = 0f;

        RenderParticles();
        MeasureFlow(Time.deltaTime);

        if (logNeighborStats && (++frameCounter % 60 == 0)) LogNeighborStats();
    }

    // M3.3: measure outflow so we can see it respond to hole size / viscosity / bucket speed.
    void MeasureFlow(float dt)
    {
        int inside = 0;
        for (int i = 0; i < positions.Length; i++)
            if (!escaped[i] && !dead[i]) inside++;
        inBucketCount = inside;

        if (dt > 1e-5f)
        {
            float instRate = (drainedCount - lastDrained) / dt;
            flowRateSmooth = Mathf.Lerp(flowRateSmooth, instRate, 0.1f);
        }
        lastDrained = drainedCount;
    }

    // Step F: draw all particles in one instanced call (no GameObject per particle).
    void RenderParticles()
    {
        if (positions == null) return;
        Vector3 scale = Vector3.one * particleDiameter;
        for (int i = 0; i < positions.Length; i++)
            matrices[i] = dead[i]
                ? Matrix4x4.TRS(positions[i], Quaternion.identity, Vector3.zero) // hidden
                : Matrix4x4.TRS(positions[i], Quaternion.identity, scale);

        var rp = new RenderParams(particleMaterial)
        {
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = false
        };
        Graphics.RenderMeshInstanced(rp, particleMesh, 0, matrices, positions.Length);
    }

    void OnGUI()
    {
        if (!showStats) return;
        fpsSmooth = Mathf.Lerp(fpsSmooth, 1f / Mathf.Max(Time.deltaTime, 1e-5f), 0.1f);
        int n = positions != null ? positions.Length : 0;
        GUI.Label(new Rect(12, 10, 520, 24),
            $"SPH  total:{n}  in-bucket:{inBucketCount}  drained:{drainedCount}  flow:{flowRateSmooth:F0}/s   FPS:{fpsSmooth:F0}");
    }

    // Step E: place the cylinder at the bucket's current transform and estimate the
    // wall's linear/angular velocity (so moving walls can push the fluid -> sloshing).
    void UpdateContainerKinematics(float dt)
    {
        if (followBucket && bucket != null)
        {
            Vector3 newCenter = bucket.TransformPoint(localContainerCenter);
            Vector3 newUp = bucket.up;

            if (dt > 1e-6f)
            {
                containerLinVel = (newCenter - prevContainerCenter) / dt;
                Quaternion dq = bucket.rotation * Quaternion.Inverse(prevBucketRot);
                dq.ToAngleAxis(out float angleDeg, out Vector3 axis);
                if (angleDeg > 180f) angleDeg -= 360f;
                if (float.IsInfinity(axis.x) || axis.sqrMagnitude < 1e-8f)
                    containerAngVel = Vector3.zero;
                else
                    containerAngVel = axis.normalized * (angleDeg * Mathf.Deg2Rad / dt);
            }

            worldContainerCenter = newCenter;
            worldUp = newUp;
            prevContainerCenter = newCenter;
            prevBucketRot = bucket.rotation;
        }
        else
        {
            worldContainerCenter = containerCenter;
            worldUp = Vector3.up;
            containerLinVel = Vector3.zero;
            containerAngVel = Vector3.zero;
        }
    }

    void Step(float dt)
    {
        BuildGrid();                 // Step B: refresh neighbor grid
        ComputeDensityPressure();    // Step C: density + pressure per particle

        float h = smoothingRadius;
        float coeff = 45f / (Mathf.PI * Mathf.Pow(h, 6)); // shared by Spiky grad & Visc Laplacian
        float maxSpeedSq = maxSpeed * maxSpeed;

        for (int i = 0; i < positions.Length; i++)
        {
            if (dead[i]) continue;

            // Pressure force (repels overlapping particles -> incompressibility)
            // Viscosity force (smooths velocity vs neighbors -> cohesive, paint-like)
            Vector3 fPress = Vector3.zero;
            Vector3 fVisc = Vector3.zero;
            Vector3 pi = positions[i];
            Vector3 vi = velocities[i];
            GetNeighbors(i, neighborScratch);
            for (int k = 0; k < neighborScratch.Count; k++)
            {
                int j = neighborScratch[k];
                Vector3 dir = pi - positions[j];
                float r = dir.magnitude;
                if (r > 1e-6f && r < h)
                {
                    Vector3 d = dir / r;
                    float spiky = coeff * (h - r) * (h - r);
                    fPress += d * (particleMass * (pressure[i] + pressure[j]) / (2f * density[j]) * spiky);

                    float lap = coeff * (h - r);
                    fVisc += (velocities[j] - vi) * (particleMass / density[j] * lap);
                }
            }
            fVisc *= viscosity;

            Vector3 a = gravity + (fPress + fVisc) / density[i];
            velocities[i] += a * dt;

            // safety clamp
            if (velocities[i].sqrMagnitude > maxSpeedSq)
                velocities[i] = velocities[i].normalized * maxSpeed;

            positions[i] += velocities[i] * dt;

            if (!escaped[i]) ResolveBoundary(i, ref positions[i], ref velocities[i]);
            if (escaped[i] && positions[i].y < despawnBelowY) dead[i] = true;
        }
    }

    // rho_i = sum_j m * W_poly6(r_ij) ;  p_i = max(0, k * (rho_i - rho0))
    void ComputeDensityPressure()
    {
        float h = smoothingRadius;
        float h2 = h * h;
        float poly6 = 315f / (64f * Mathf.PI * Mathf.Pow(h, 9));

        for (int i = 0; i < positions.Length; i++)
        {
            float rho = particleMass * poly6 * (h2 * h2 * h2); // self contribution (r=0)
            Vector3 pi = positions[i];
            GetNeighbors(i, neighborScratch);
            for (int k = 0; k < neighborScratch.Count; k++)
            {
                int j = neighborScratch[k];
                float r2 = (positions[j] - pi).sqrMagnitude;
                if (r2 < h2)
                {
                    float x = h2 - r2;
                    rho += particleMass * poly6 * x * x * x;
                }
            }
            density[i] = Mathf.Max(rho, 1e-5f);
            pressure[i] = Mathf.Max(0f, stiffness * (density[i] - restDensity));
        }
    }

    float AverageDensity()
    {
        float h = smoothingRadius;
        float h2 = h * h;
        float poly6 = 315f / (64f * Mathf.PI * Mathf.Pow(h, 9));
        float total = 0f;
        for (int i = 0; i < positions.Length; i++)
        {
            float rho = particleMass * poly6 * (h2 * h2 * h2);
            Vector3 pi = positions[i];
            GetNeighbors(i, neighborScratch);
            for (int k = 0; k < neighborScratch.Count; k++)
            {
                float r2 = (positions[neighborScratch[k]] - pi).sqrMagnitude;
                if (r2 < h2) { float x = h2 - r2; rho += particleMass * poly6 * x * x * x; }
            }
            total += rho;
        }
        return positions.Length > 0 ? total / positions.Length : restDensity;
    }

    // Reflect particles off an ORIENTED cylinder (floor closed, top open) that follows
    // the bucket. Velocity is reflected relative to the moving wall, so a swinging
    // bucket drags the fluid and produces sloshing.
    void ResolveBoundary(int i, ref Vector3 p, ref Vector3 v)
    {
        float r = particleRadius;
        Vector3 up = worldUp;

        Vector3 rel = p - worldContainerCenter;
        float axial = Vector3.Dot(rel, up);
        Vector3 radialVec = rel - axial * up;
        float radial = radialVec.magnitude;
        float maxR = containerRadius - r;

        // Side wall — only where the bucket walls actually are (within its height).
        // Below the floor (drained through the hole) there is no wall, so paint falls free.
        if (axial >= -containerHalfHeight && axial <= containerHalfHeight
            && radial > maxR && radial > 1e-6f)
        {
            Vector3 inward = -(radialVec / radial);
            p += inward * (radial - maxR);
            ReflectVel(ref v, p, inward);

            rel = p - worldContainerCenter;
            axial = Vector3.Dot(rel, up);
            radialVec = rel - axial * up;
            radial = radialVec.magnitude;
        }

        // Bottom — closed, EXCEPT a hole at the center the paint can drain through.
        float minAxial = -containerHalfHeight + r;
        if (axial < minAxial)
        {
            bool overHole = holeOpen && radial < holeDiameter * 0.5f;
            if (!overHole)
            {
                p += up * (minAxial - axial);
                ReflectVel(ref v, p, up);
            }
            else
            {
                escaped[i] = true; // drained through the hole -> now free-falling paint
                drainedCount++;
            }
        }
        // Top is open.
    }

    // Reflect velocity about 'inwardNormal' relative to the wall's velocity at point p.
    void ReflectVel(ref Vector3 v, Vector3 p, Vector3 inwardNormal)
    {
        Vector3 wallVel = containerLinVel + Vector3.Cross(containerAngVel, p - worldContainerCenter);
        Vector3 vRel = v - wallVel;
        float vn = Vector3.Dot(vRel, inwardNormal);
        if (vn < 0f) vRel -= (1f + boundaryDamping) * vn * inwardNormal; // bounce inward
        v = wallVel + vRel;
    }

    // ----- Step B: spatial hash neighbor search -----

    Vector3Int CellOf(Vector3 p)
    {
        float inv = 1f / Mathf.Max(smoothingRadius, 1e-4f);
        return new Vector3Int(
            Mathf.FloorToInt(p.x * inv),
            Mathf.FloorToInt(p.y * inv),
            Mathf.FloorToInt(p.z * inv));
    }

    // Rebuild the grid from current positions. Lists are pooled to limit GC.
    void BuildGrid()
    {
        foreach (var list in grid.Values) { list.Clear(); listPool.Push(list); }
        grid.Clear();

        for (int i = 0; i < positions.Length; i++)
        {
            if (dead != null && dead[i]) continue; // dead particles leave the simulation
            Vector3Int c = CellOf(positions[i]);
            if (!grid.TryGetValue(c, out var list))
            {
                list = listPool.Count > 0 ? listPool.Pop() : new List<int>(16);
                grid[c] = list;
            }
            list.Add(i);
        }
    }

    // Fill 'result' with indices within smoothingRadius of particle i (excludes i).
    void GetNeighbors(int i, List<int> result)
    {
        result.Clear();
        Vector3 pi = positions[i];
        Vector3Int c = CellOf(pi);
        float h2 = smoothingRadius * smoothingRadius;

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            var cell = new Vector3Int(c.x + dx, c.y + dy, c.z + dz);
            if (!grid.TryGetValue(cell, out var list)) continue;
            for (int k = 0; k < list.Count; k++)
            {
                int j = list[k];
                if (j == i) continue;
                if ((positions[j] - pi).sqrMagnitude <= h2) result.Add(j);
            }
        }
    }

    void LogNeighborStats()
    {
        BuildGrid();
        int total = 0, min = int.MaxValue, max = 0;
        for (int i = 0; i < positions.Length; i++)
        {
            GetNeighbors(i, neighborScratch);
            int n = neighborScratch.Count;
            total += n;
            if (n < min) min = n;
            if (n > max) max = n;
        }
        float avg = positions.Length > 0 ? (float)total / positions.Length : 0f;
        Debug.Log($"[SPH][StepB] neighbors  avg={avg:F1}  min={min}  max={max}  (h={smoothingRadius}, N={positions.Length})");
    }

    bool ComputeContainerFromBucket()
    {
        if (bucket == null) { Debug.LogError("[SPH] No 'bucket' reference set."); return false; }
        var renderers = bucket.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Debug.LogError("[SPH] Bucket has no Renderer."); return false; }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        containerCenter = b.center;
        containerRadius = Mathf.Min(b.size.x, b.size.z) * 0.5f * innerRadiusFactor;
        containerBottomY = b.center.y - b.size.y * 0.5f;
        containerTopY = b.center.y + b.size.y * 0.5f;
        containerHalfHeight = b.size.y * 0.5f;

        // Express the center in bucket-local space so the cylinder can follow the bucket.
        localContainerCenter = bucket.InverseTransformPoint(containerCenter);
        worldContainerCenter = containerCenter;
        prevContainerCenter = containerCenter;
        prevBucketRot = bucket.rotation;
        worldUp = Vector3.up;
        return true;
    }

    void EnsureMaterial()
    {
        if (particleMaterial != null) return;
        var sh = Shader.Find("Custom/LiquidPaint");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        particleMaterial = new Material(sh);
    }

    void SpawnParticles()
    {
        positions = new Vector3[particleCount];
        velocities = new Vector3[particleCount];
        density = new float[particleCount];
        pressure = new float[particleCount];
        matrices = new Matrix4x4[particleCount];
        escaped = new bool[particleCount];
        dead = new bool[particleCount];

        if (particleMaterial != null) particleMaterial.enableInstancing = true;
        particleDiameter = particleRadius * 2f * particleVisualScale;

        // Build an orthonormal basis around the (possibly tilted) container axis.
        Vector3 up = worldUp;
        Vector3 right = Vector3.Cross(up, Vector3.forward);
        if (right.sqrMagnitude < 1e-4f) right = Vector3.Cross(up, Vector3.right);
        right.Normalize();
        Vector3 fwd = Vector3.Cross(right, up).normalized;

        float spacing = particleRadius * 2.1f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((containerRadius * 1.4f) / spacing));
        float start = -(perRow - 1) * spacing * 0.5f;
        Vector3 top = worldContainerCenter + up * (containerHalfHeight - spacing); // near the top

        for (int i = 0; i < particleCount; i++)
        {
            int rem = i % (perRow * perRow);
            int layer = i / (perRow * perRow);
            int gx = rem % perRow;
            int gz = rem / perRow;

            positions[i] = top
                + right * (start + gx * spacing)
                + fwd * (start + gz * spacing)
                - up * (layer * spacing);
            velocities[i] = Vector3.zero;
        }
    }

    // Procedural UV sphere (double-sided so winding can never hide it). Radius given.
    static Mesh BuildSphereMesh(int longitude, int latitude, float radius)
    {
        longitude = Mathf.Max(4, longitude);
        latitude = Mathf.Max(3, latitude);

        int vertCount = (longitude + 1) * (latitude + 1);
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        int vi = 0;
        for (int lat = 0; lat <= latitude; lat++)
        {
            float theta = (float)lat / latitude * Mathf.PI;
            float st = Mathf.Sin(theta), ct = Mathf.Cos(theta);
            for (int lon = 0; lon <= longitude; lon++)
            {
                float phi = (float)lon / longitude * Mathf.PI * 2f;
                Vector3 n = new Vector3(st * Mathf.Cos(phi), ct, st * Mathf.Sin(phi));
                verts[vi] = n * radius;
                norms[vi] = n;
                vi++;
            }
        }

        var tris = new int[longitude * latitude * 12]; // x2 for double-sided
        int ti = 0;
        for (int lat = 0; lat < latitude; lat++)
        {
            for (int lon = 0; lon < longitude; lon++)
            {
                int cur = lat * (longitude + 1) + lon;
                int next = cur + longitude + 1;

                tris[ti++] = cur;     tris[ti++] = next;     tris[ti++] = cur + 1;
                tris[ti++] = cur + 1; tris[ti++] = next;     tris[ti++] = next + 1;
                // reversed winding (back faces)
                tris[ti++] = cur + 1; tris[ti++] = next;     tris[ti++] = cur;
                tris[ti++] = next + 1; tris[ti++] = next;    tris[ti++] = cur + 1;
            }
        }

        var m = new Mesh { name = "SphParticle" };
        m.vertices = verts;
        m.normals = norms;
        m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }
}
