using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum HitboxShape { Sphere, Box }

[System.Serializable]
public class HitboxKey
{
    public string hitboxId;
    public string bonePath;           // Transform path from rig root
    public int startFrame;
    public int endFrame;
    public Vector3 localPosition;     // At startFrame
    public Quaternion localRotation;  // At startFrame
    public Vector3 localScale;        // At startFrame
    public string bodyGroup;          // Grouped body region (Head/Torso/Arm.L/etc.)

    // Visualization/shape authoring (editor + runtime)
    public HitboxShape shape = HitboxShape.Sphere;
    public float radius = 0.2f;                   // For Sphere
    public Vector3 boxSize = new Vector3(0.2f, 0.2f, 0.2f); // For Box (local units)

    // Enable/disable this hitbox window (authoring + runtime)
    public bool enabled = true;
}

[CreateAssetMenu(menuName = "Hitbox/Hitbox Clip Data", fileName = "HitboxClipData")]
public class HitboxClipData : ScriptableObject
{
    public AnimationClip clip;
    public string rigRootName;     // Name of Animator root GameObject for path resolution
    public float frameRate;
    public List<HitboxKey> keys = new List<HitboxKey>();
}

#if UNITY_EDITOR


public static class HitboxBakeUtil
{
    public static string GetTransformPath(Transform root, Transform t)
    {
        if (t == null || root == null) return string.Empty;
        var names = new System.Collections.Generic.List<string>();
        var cur = t;
        while (cur != null && cur != root)
        {
            names.Add(cur.name);
            cur = cur.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    public static bool ShouldSkipBone(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("hitbox")) return true;
        if (n.Contains("unused") || n.Contains("adj") || n.Contains("attached")) return true;
        if (n.Contains("middle") || n.Contains("pinky") || n.Contains("finger") || n.Contains("index") || n.Contains("ring")) return true;
        return false;
    }

    public static float Percentile(List<float> samples, float p)
    {
        if (samples == null || samples.Count == 0) return 0f;
        samples.Sort();
        p = Mathf.Clamp01(p);
        float idx = (samples.Count - 1) * p;
        int lo = Mathf.FloorToInt(idx);
        int hi = Mathf.CeilToInt(idx);
        if (lo == hi) return samples[lo];
        float t = idx - lo;
        return Mathf.Lerp(samples[lo], samples[hi], t);
    }
    // Map a bone name to a coarse body group
    public static string BodyGroupFor(string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return "Other";
        var n = boneName.ToLowerInvariant();

        if (n.Contains("head")) return "Head";
        if (n.Contains("neck")) return "Neck";
        if (n.Contains("spine") || n.Contains("chest") || n.Contains("hips") || n.Contains("pelvis") || n.Contains("torso")) return "Torso";

        bool isLeft = n.Contains(" left") || n.Contains("_l") || n.Contains(".l") || n.EndsWith("l") || n.Contains("l_");
        bool isRight = n.Contains(" right") || n.Contains("_r") || n.Contains(".r") || n.EndsWith("r") || n.Contains("r_");

        if (n.Contains("shoulder") || n.Contains("clav")) return isLeft ? "Shoulder.L" : isRight ? "Shoulder.R" : "Shoulder";
        if (n.Contains("upperarm") || (n.Contains("arm") && !n.Contains("fore"))) return isLeft ? "Arm.L" : isRight ? "Arm.R" : "Arm";
        if (n.Contains("forearm")) return isLeft ? "Forearm.L" : isRight ? "Forearm.R" : "Forearm";
        if (n.Contains("hand") || n.Contains("wrist")) return isLeft ? "Hand.L" : isRight ? "Hand.R" : "Hand";

        if (n.Contains("thigh") || (n.Contains("leg") && !n.Contains("shin") && !n.Contains("calf"))) return isLeft ? "Thigh.L" : isRight ? "Thigh.R" : "Thigh";
        if (n.Contains("shin") || n.Contains("calf")) return isLeft ? "Shin.L" : isRight ? "Shin.R" : "Shin";
        if (n.Contains("foot") || n.Contains("ankle")) return isLeft ? "Foot.L" : isRight ? "Foot.R" : "Foot";

        return "Other";
    }
}
 
public class HitboxBakerWindow : EditorWindow
{
    private GameObject targetModel;
    private AnimationClip clip;
    private float movementThreshold = 0.05f;
    private float deactivationThreshold = 0.02f;
    private bool autoThreshold = true;
    private string saveFolder = "Assets/HitboxData";

    [MenuItem("Tools/Hitbox Editor/Open Hitbox Baker")]
    public static void Open()
    {
        GetWindow<HitboxBakerWindow>("Hitbox Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Hitbox Baker (Editor-only)", EditorStyles.boldLabel);
        targetModel = (GameObject)EditorGUILayout.ObjectField("Target Model (Animator Root)", targetModel, typeof(GameObject), true);
        clip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", clip, typeof(AnimationClip), false);

        autoThreshold = EditorGUILayout.Toggle("Auto Thresholds", autoThreshold);
        EditorGUI.BeginDisabledGroup(autoThreshold);
        movementThreshold = EditorGUILayout.FloatField("Movement Threshold", movementThreshold);
        deactivationThreshold = EditorGUILayout.FloatField("Deactivation Threshold", deactivationThreshold);
        EditorGUI.EndDisabledGroup();

        if (autoThreshold)
        {
            EditorGUILayout.HelpBox("Auto Thresholds: computes movement/deactivation from clip motion (p90/p50).", MessageType.Info);
        }

        saveFolder = EditorGUILayout.TextField("Save Folder", saveFolder);

        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(targetModel == null || clip == null);
        if (GUILayout.Button("Analyze + Bake HitboxClipData Asset"))
        {
            BakeAsset();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void BakeAsset()
    {
        var animator = targetModel != null ? targetModel.GetComponent<Animator>() : null;
        if (animator == null)
        {
            EditorUtility.DisplayDialog("Hitbox Baker", "Target Model must have an Animator component.", "OK");
            return;
        }
        if (clip == null)
        {
            EditorUtility.DisplayDialog("Hitbox Baker", "Assign an AnimationClip to bake.", "OK");
            return;
        }

        // Prepare sampling
        float fps = Mathf.Max(1f, clip.frameRate);
        int totalFrames = Mathf.CeilToInt(clip.length * fps);
        var bones = animator.GetComponentsInChildren<Transform>(true);

        // Auto thresholds pre-pass if enabled
        if (autoThreshold)
        {
            ComputeAutoThresholds(animator, clip, fps, bones, out float autoMove, out float autoDeact);
            movementThreshold = autoMove;
            deactivationThreshold = autoDeact;
        }

        // Track previous positions per bone to compute movement magnitude
        var lastWorldPos = new Dictionary<Transform, Vector3>(bones.Length);
        var activeByBone = new Dictionary<Transform, bool>(bones.Length);
        var startFrameByBone = new Dictionary<Transform, int>(bones.Length);
        var currentKeyIdByBone = new Dictionary<Transform, string>(bones.Length);

        foreach (var b in bones)
        {
            lastWorldPos[b] = b.position;
            activeByBone[b] = false;
            startFrameByBone[b] = -1;
            currentKeyIdByBone[b] = null;
        }

        var baked = ScriptableObject.CreateInstance<HitboxClipData>();
        baked.clip = clip;
        baked.rigRootName = animator.gameObject.name;
        baked.frameRate = fps;

        // Ensure folder exists (robust + normalized under Assets)
        var normalized = saveFolder.Replace("\\", "/").Trim('/');
        if (!normalized.StartsWith("Assets"))
            normalized = "Assets/" + normalized;

        var parts = normalized.Split('/');
        var current = parts[0]; // should be "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
        saveFolder = normalized;

        // Sample the clip across frames
        for (int frame = 0; frame <= totalFrames; frame++)
        {
            float t = Mathf.Min(clip.length, frame / fps);
            clip.SampleAnimation(targetModel, t);

            foreach (var bone in bones)
            {
                if (bone == null) continue;
                if (bone == animator.transform) continue; // skip root object itself
                if (HitboxBakeUtil.ShouldSkipBone(bone.name)) continue;

                // Movement magnitude since last frame
                var lw = lastWorldPos[bone];
                var cur = bone.position;
                var movementMag = (cur - lw).magnitude;
                lastWorldPos[bone] = cur;

                // Detect start
                if (!activeByBone[bone] && movementMag > movementThreshold)
                {
                    activeByBone[bone] = true;
                    startFrameByBone[bone] = frame;

                    var id = $"{bone.parent?.name ?? bone.name}_{clip.name}_{frame}";
                    currentKeyIdByBone[bone] = id;

                    // Record transform at start (local to parent)
                    var hitboxBone = bone.parent != null ? bone.parent : bone;
                    var group = HitboxBakeUtil.BodyGroupFor(hitboxBone.name);
                    var defaultRadius = DefaultRadiusForGroup(group);
                    var key = new HitboxKey
                    {
                        hitboxId = id,
                        bonePath = HitboxBakeUtil.GetTransformPath(animator.transform, hitboxBone),
                        startFrame = frame,
                        endFrame = frame, // will be updated upon end
                        localPosition = (bone.parent != null ? bone.parent.InverseTransformPoint(bone.position) : bone.localPosition),
                        localRotation = (bone.parent != null ? Quaternion.Inverse(bone.parent.rotation) * bone.rotation : bone.localRotation),
                        localScale = (bone.parent != null ? Vector3.Scale(bone.localScale, Vector3.one) : bone.localScale),
                        bodyGroup = group,
                        shape = HitboxShape.Sphere,
                        radius = defaultRadius,
                        boxSize = new Vector3(defaultRadius, defaultRadius, defaultRadius)
                    };
                    baked.keys.Add(key);
                }
                // Detect end
                else if (activeByBone[bone] && movementMag < deactivationThreshold)
                {
                    activeByBone[bone] = false;
                    var id = currentKeyIdByBone[bone];
                    // find last key for this id and set endFrame
                    for (int i = baked.keys.Count - 1; i >= 0; i--)
                    {
                        if (baked.keys[i].hitboxId == id)
                        {
                            baked.keys[i].endFrame = frame;
                            break;
                        }
                    }
                    currentKeyIdByBone[bone] = null;
                }
            }
        }

        // Close any windows that never deactivated (set end to last frame)
        for (int i = 0; i < baked.keys.Count; i++)
        {
            if (baked.keys[i].endFrame < baked.keys[i].startFrame)
            {
                baked.keys[i].endFrame = totalFrames;
            }
        }

        // Save asset
        var safeClipName = clip.name.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
        var assetPath = $"{saveFolder}/{animator.gameObject.name}_{safeClipName}_HitboxClipData.asset";
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
        AssetDatabase.CreateAsset(baked, assetPath);
        EditorUtility.SetDirty(baked);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(baked);

        EditorUtility.DisplayDialog(
            "Hitbox Baker",
            $"Baked {baked.keys.Count} hitbox windows to:\n{assetPath}\n\nThresholds Used:\n  Movement: {movementThreshold:F4}\n  Deactivation: {deactivationThreshold:F4}",
            "OK"
        );
    }

    // Computes thresholds based on clip motion distribution:
    // - movementThreshold = 90th percentile of per-frame per-bone deltas
    // - deactivationThreshold = 50th percentile
    private void ComputeAutoThresholds(Animator animator, AnimationClip clip, float fps, Transform[] bones, out float move, out float deact)
    {
        var deltas = new List<float>(bones.Length * Mathf.CeilToInt(clip.length * fps));

        // Use a dictionary of last positions for this pre-pass to avoid interfering with the main bake
        var lastPos = new Dictionary<Transform, Vector3>(bones.Length);
        foreach (var b in bones) lastPos[b] = b.position;

        int totalFrames = Mathf.CeilToInt(clip.length * fps);
        for (int frame = 0; frame <= totalFrames; frame++)
        {
            float t = Mathf.Min(clip.length, frame / fps);
            clip.SampleAnimation(animator.gameObject, t);

            foreach (var bone in bones)
            {
                if (bone == null) continue;
                if (bone == animator.transform) continue;
                if (HitboxBakeUtil.ShouldSkipBone(bone.name)) continue;

                var prev = lastPos[bone];
                var cur = bone.position;
                float d = (cur - prev).magnitude;
                lastPos[bone] = cur;
                deltas.Add(d);
            }
        }

        // Fallbacks in case of no motion
        if (deltas.Count == 0)
        {
            move = 0.05f;
            deact = 0.02f;
            return;
        }

        move = HitboxBakeUtil.Percentile(deltas, 0.90f);
        deact = Mathf.Min(move * 0.5f, HitboxBakeUtil.Percentile(deltas, 0.50f));
        // Avoid degenerate thresholds
        if (move < 1e-5f) move = 0.05f;
        if (deact < 1e-6f) deact = Mathf.Min(0.02f, move * 0.5f);
    }

    // Sensible default radius per body group
    private static float DefaultRadiusForGroup(string group)
    {
        switch (group)
        {
            case "Head": return 0.24f;
            case "Neck": return 0.18f;
            case "Torso": return 0.32f;
            case "Shoulder.L":
            case "Shoulder.R": return 0.20f;
            case "Arm.L":
            case "Arm.R": return 0.16f;
            case "Forearm.L":
            case "Forearm.R": return 0.14f;
            case "Hand.L":
            case "Hand.R": return 0.12f;
            case "Thigh.L":
            case "Thigh.R": return 0.20f;
            case "Shin.L":
            case "Shin.R": return 0.16f;
            case "Foot.L":
            case "Foot.R": return 0.12f;
            default: return 0.16f;
        }
    }
}
#endif