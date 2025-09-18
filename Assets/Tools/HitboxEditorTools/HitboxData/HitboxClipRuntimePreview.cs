using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime/Play Mode preview for baked HitboxClipData.
/// - Spawns hitbox prefabs parented to the correct bones using stored local pose
/// - Toggles them on/off based on current Animator time mapped to frames
/// - Optionally draws gizmos for visual debugging
/// Attach this to your character (Animator root), assign data + prefab, and press Play.
/// </summary>
[DisallowMultipleComponent]
public class HitboxClipRuntimePreview : MonoBehaviour
{
    [Header("Data & Animator")]
    public HitboxClipData data;
    public Animator animator;

    [Header("Hitbox Prefab")]
    public GameObject hitboxPrefab;

    [Header("Visualization")]
    public bool sizeFromShape = true; // Scale instances from HitboxKey.shape (Sphere radius / Box size)
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    public float gizmoSize = 0.06f;   // only used if not sizing from shape on gizmos
    public bool drawOrigins = true;
    public Color originColor = new Color(1f, 0.95f, 0.1f, 1f);

    private readonly Dictionary<HitboxKey, GameObject> instances = new Dictionary<HitboxKey, GameObject>();
    private readonly Dictionary<HitboxKey, Transform> targetBones = new Dictionary<HitboxKey, Transform>();

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    private void Start()
    {
        PrepareInstances();
    }

    private void OnValidate()
    {
        // Re-resolve animator reference if missing
        if (animator == null) animator = GetComponent<Animator>();
    }

    private void PrepareInstances()
    {
        ClearInstances();

        if (data == null)
        {
            Debug.LogWarning("[HitboxClipRuntimePreview] No HitboxClipData assigned.");
            return;
        }
        if (animator == null)
        {
            Debug.LogWarning("[HitboxClipRuntimePreview] No Animator found.");
            return;
        }
        if (hitboxPrefab == null)
        {
            Debug.LogWarning("[HitboxClipRuntimePreview] No hitboxPrefab assigned (visuals will be skipped).");
        }

        // Resolve bone targets
        targetBones.Clear();
        foreach (var key in data.keys)
        {
            if (key == null || !key.enabled) continue;

            Transform bone = ResolveBone(animator.transform, key.bonePath);
            if (bone == null)
            {
                Debug.LogWarning($"[HitboxClipRuntimePreview] Could not resolve bone path '{key.bonePath}'.");
                continue;
            }

            targetBones[key] = bone;

            if (hitboxPrefab != null)
            {
                var go = Instantiate(hitboxPrefab, bone, false);
                go.name = $"hitbox_{key.hitboxId}";

                // Apply stored local pose at the window start
                go.transform.localPosition = key.localPosition;
                go.transform.localRotation = key.localRotation;

                // Size instance to match authored shape (accurate to runtime intent)
                if (sizeFromShape)
                {
                    if (key.shape == HitboxShape.Sphere)
                    {
                        // Assuming prefab unit sphere diameter = 1
                        go.transform.localScale = Vector3.one * (key.radius * 2f);
                    }
                    else
                    {
                        // Assuming prefab unit cube size = 1
                        go.transform.localScale = key.boxSize;
                    }
                }
                else
                {
                    // Legacy: use stored localScale if you prefer that behavior
                    go.transform.localScale = key.localScale;
                }

                go.SetActive(false);
                instances[key] = go;
            }
        }
    }

    private void ClearInstances()
    {
        if (instances.Count == 0) return;
        foreach (var kvp in instances)
        {
            if (kvp.Value != null)
            {
                if (Application.isPlaying) Destroy(kvp.Value);
                else DestroyImmediate(kvp.Value);
            }
        }
        instances.Clear();
    }

    private void OnDisable()
    {
        ClearInstances();
    }

    private void Update()
    {
        if (data == null || animator == null) return;

        // Determine current clip and time for layer 0
        var frame = ComputeDataFrame(animator, data, 0, out bool clipMatches);
        // Toggle spawned instances if we have visuals
        if (instances.Count > 0)
        {
            foreach (var kvp in instances)
            {
                var key = kvp.Key;
                bool active = clipMatches && frame >= key.startFrame && frame <= key.endFrame;
                var go = kvp.Value;
                if (go != null && go.activeSelf != active) go.SetActive(active);
            }
        }
    }

    private int ComputeDataFrame(Animator a, HitboxClipData d, int layer, out bool clipMatches)
    {
        clipMatches = false;
        if (a == null || d == null || d.clip == null) return 0;

        var infos = a.GetCurrentAnimatorClipInfo(layer);
        var state = a.GetCurrentAnimatorStateInfo(layer);

        if (infos == null || infos.Length == 0)
        {
            // No clip info; assume start
            return 0;
        }

        var currentClip = infos[0].clip;
        if (currentClip == null)
            return 0;

        // Only drive windows when the baked clip is the currently playing one
        clipMatches = currentClip == d.clip || currentClip.name == d.clip.name;

        // Convert normalized time to seconds; handle looping
        float length = currentClip.length;
        float norm = state.normalizedTime;
        float looped = norm - Mathf.Floor(norm); // 0..1
        float time = looped * length;

        // Convert to frame using baked framerate
        int frame = Mathf.Clamp(Mathf.RoundToInt(time * Mathf.Max(1f, d.frameRate)), 0, int.MaxValue);
        return frame;
    }

    private Transform ResolveBone(Transform root, string bonePath)
    {
        if (root == null || string.IsNullOrEmpty(bonePath)) return null;
        // Path stored is relative to animator root
        // Transform.Find supports slash-separated child paths
        return root.Find(bonePath);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (data == null || animator == null) return;

        // Draw accurate shapes at active windows (based on current frame)
        var frame = ComputeDataFrame(animator, data, 0, out bool clipMatches);
        if (!clipMatches) return;

        foreach (var key in data.keys)
        {
            if (key == null || !key.enabled) continue;
            if (frame < key.startFrame || frame > key.endFrame) continue;

            var bone = ResolveBone(animator.transform, key.bonePath);
            if (bone == null) continue;

            var worldPos = bone.TransformPoint(key.localPosition);
            var worldRot = bone.rotation * key.localRotation;
            var lossy = bone.lossyScale;

            // Draw origin dot
            if (drawOrigins)
            {
                var prev = Gizmos.color;
                Gizmos.color = originColor;
                // small, view-independent-ish dot
                Gizmos.DrawSphere(worldPos, 0.01f);
                Gizmos.color = prev;
            }

            // Draw shape sized to match runtime
            Gizmos.color = gizmoColor;
            if (key.shape == HitboxShape.Sphere)
            {
                float maxScale = Mathf.Max(lossy.x, Mathf.Max(lossy.y, lossy.z));
                float r = sizeFromShape ? (key.radius * maxScale) : gizmoSize;
                Gizmos.DrawWireSphere(worldPos, r);
            }
            else
            {
                Vector3 worldSize = sizeFromShape ? Vector3.Scale(key.boxSize, lossy) : Vector3.one * gizmoSize;

                var prevMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(worldPos, worldRot, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, worldSize);
                Gizmos.matrix = prevMatrix;
            }
        }
    }
}