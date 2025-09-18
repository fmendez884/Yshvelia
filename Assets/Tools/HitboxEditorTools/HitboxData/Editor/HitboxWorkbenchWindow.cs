#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class HitboxWorkbenchWindow : EditorWindow
{
    // Selection + data
    private GameObject targetModel;
    private Animator animator;
    private AnimationClip selectedClip;
    private HitboxClipData data;

    // Discovery
    private Vector2 clipListScroll;
    private List<AnimationClip> discoveredClips = new List<AnimationClip>();
    private string saveFolder = "Assets/HitboxData";
    private string search = "";

    // Layout and UX
    private bool simpleMode = true;
    private bool showClipsPanel = true;
    private bool showKeysPanel = true;

    // Preview
    private bool previewMode = false;
    private bool isPlaying = false;
    private double lastEditorTime;
    private float previewTimeSec = 0f;
    private float fps = 60f;
    private int totalFrames = 0;

    // Keys UI
    private Vector2 keysScroll;
    private int selectedKeyIndex = -1;
    private Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
    private string currentBonePathInput = "";
    private bool groupByBodyPart = true;
    private string filterGroup = "All";

    // Scene draw
    private bool drawSceneGizmos = true;
    private Color gizmoColor = new Color(1f, 0.25f, 0.25f, 0.9f);
    private Color selectedGizmoColor = new Color(0.2f, 0.8f, 1f, 1f);
private Color previewGizmoColor = new Color(0.2f, 0.9f, 0.9f, 0.95f);
    private bool scaleGizmosFromShape = true;
    private float gizmoSize = 0.03f; // used when not scaling from shape
    private bool drawOrigins = true;  // draw small dots at hitbox origins
    private Color originColor = new Color(1f, 0.95f, 0.1f, 1f);
    private float gizmoVisualScale = 0.15f; // non-destructive visual-only scale

    // Activation threshold settings
    private bool autoThresholds = true;
    private float movementThreshold = 0.05f;
    private float deactivationThreshold = 0.02f;
    // Auto mode tuning
    private float movementPercentile = 0.90f;      // 0..1
    private float deactivationPercentile = 0.50f;  // 0..1
    private float deactRatioOfMove = 0.50f;        // deactivation <= move * this

    // Group filtering
    private bool includeAllGroups = false;

    // Motion analysis options
    private bool useLocalDeltas = true;    // measure deltas in local space to parent to reduce root/scene noise
    private int smoothWindow = 3;          // frames for EMA smoothing (1 = off)
    private int minWindowFrames = 4;       // discard windows shorter than this
    private int minGapFrames = 4;          // cooldown frames after deactivation before allowing reactivation

    // Simple mode control
    private float sensitivity = 0.75f;     // 0..1, higher = stricter (fewer hitboxes)
    private bool advancedFoldout = false;  // foldout to reveal advanced settings in Simple mode
    // Generation (unify tools in one window)
    private GameObject generationHitboxPrefab;
    private bool addAnimationEvents = true;

    // Bulk resize UI state
    private Dictionary<string, float> groupScaleUI = new Dictionary<string, float>();
    private float globalScaleUI = 1f;

// Live threshold preview (no save)
private bool liveThresholdPreview = false;
private List<HitboxKey> previewKeys = new List<HitboxKey>();

// Change caches for incremental rebuilds
private bool lastAutoThresholds = true;
private float lastMovementThreshold = 0.05f;
private float lastDeactivationThreshold = 0.02f;
private float lastMovementPercentile = 0.90f;
private float lastDeactivationPercentile = 0.50f;
private float lastDeactRatioOfMove = 0.50f;
private float lastComputedMove = 0f;
private float lastComputedDeact = 0f;
private AnimationClip lastSelectedClip = null;
private GameObject lastTargetModel = null;

// Live preview change caches (extras)
private bool lastUseLocalDeltas = true;
private int lastSmoothWindow = 3;
private int lastMinWindowFrames = 4;
private int lastMinGapFrames = 4;

    [MenuItem("Tools/Hitbox Editor/Hitbox Workbench")]
    public static void Open()
    {
        GetWindow<HitboxWorkbenchWindow>("Hitbox Workbench");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += EditorUpdate;
    }

    private void OnDisable()
    {
        if (previewMode) AnimationMode.StopAnimationMode();
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= EditorUpdate;
    }

    private void EditorUpdate()
    {
        if (!isPlaying || !previewMode || selectedClip == null || targetModel == null) return;

        var now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - lastEditorTime);
        lastEditorTime = now;

        previewTimeSec += delta;
        if (previewTimeSec > selectedClip.length) previewTimeSec = 0f;
        SamplePreview();
        Repaint();
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hitbox Workbench", EditorStyles.boldLabel);

        // Top toolbar
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            simpleMode = GUILayout.Toggle(simpleMode, "Simple", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.Space(4);
            showClipsPanel = GUILayout.Toggle(showClipsPanel, "Clips", EditorStyles.toolbarButton, GUILayout.Width(60));
            showKeysPanel = GUILayout.Toggle(showKeysPanel, "Keys", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Find Clips (Animator)", EditorStyles.toolbarButton))
                DiscoverClipsFromAnimator();
            if (GUILayout.Button("Find Clips (Project)", EditorStyles.toolbarButton))
                DiscoverClipsInProject();
            if (GUILayout.Button("Batch Bake (Animator)", EditorStyles.toolbarButton))
                BatchBakeFromAnimator();
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            targetModel = (GameObject)EditorGUILayout.ObjectField("Target Model (Animator Root)", targetModel, typeof(GameObject), true);
            if (targetModel != null)
            {
                animator = targetModel.GetComponent<Animator>();
                if (animator == null)
                    EditorGUILayout.HelpBox("No Animator component found on target model.", MessageType.Warning);
            }

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Save Folder", GUILayout.Width(80));
                saveFolder = EditorGUILayout.TextField(saveFolder);
                GUILayout.FlexibleSpace();
                search = EditorGUILayout.TextField("Search", search);
            }
        }

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (showClipsPanel) DrawClipListPanel();
            if (showClipsPanel) EditorGUILayout.Space(4);
            DrawMainPanel();
            if (showKeysPanel) { EditorGUILayout.Space(4); DrawKeysPanel(); }
        }
    }

    private void DrawClipListPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(220), GUILayout.MaxWidth(340)))
        {
            EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);
            clipListScroll = EditorGUILayout.BeginScrollView(clipListScroll);
            foreach (var clip in discoveredClips)
            {
                if (!string.IsNullOrEmpty(search) && !clip.name.ToLowerInvariant().Contains(search.ToLowerInvariant()))
                    continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(clip.name, (selectedClip == clip) ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                    {
                        SelectClip(clip);
                    }
                    if (GUILayout.Button("Load Data", EditorStyles.miniButtonRight, GUILayout.Width(80)))
                    {
                        SelectClip(clip);
                        LoadOrCreateDataForSelection();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawMainPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(320), GUILayout.ExpandWidth(true)))
        {
            EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
            selectedClip = (AnimationClip)EditorGUILayout.ObjectField("Clip", selectedClip, typeof(AnimationClip), false);
            data = (HitboxClipData)EditorGUILayout.ObjectField("Data", data, typeof(HitboxClipData), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load/Create Data", GUILayout.Width(150)))
                {
                    LoadOrCreateDataForSelection();
                }
                if (GUILayout.Button("Bake From Motion", GUILayout.Width(150)))
                {
                    BakeFromMotion();
                }
                if (GUILayout.Button("Save Asset", GUILayout.Width(120)))
                {
                    SaveDataAsset();
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                previewMode = EditorGUILayout.ToggleLeft("Preview Mode", previewMode, GUILayout.Width(110));
                EditorGUI.BeginDisabledGroup(!previewMode);
                if (GUILayout.Button(isPlaying ? "Pause" : "Play", GUILayout.Width(60)))
                {
                    TogglePlay();
                }
                if (GUILayout.Button("Reset", GUILayout.Width(60)))
                {
                    previewTimeSec = 0f;
                    SamplePreview();
                }
                EditorGUI.EndDisabledGroup();
            }

            if (selectedClip != null)
            {
                fps = Mathf.Max(1f, selectedClip.frameRate);
                totalFrames = Mathf.CeilToInt(selectedClip.length * fps);
                EditorGUI.BeginDisabledGroup(!previewMode || selectedClip == null || targetModel == null);
                {
                    int frame = Mathf.RoundToInt(previewTimeSec * fps);
                    int newFrame = EditorGUILayout.IntSlider("Frame", frame, 0, Math.Max(0, totalFrames));
                    if (newFrame != frame)
                    {
                        previewTimeSec = Mathf.Clamp(newFrame / fps, 0f, selectedClip.length);
                        SamplePreview();
                    }
                    float newTime = EditorGUILayout.Slider("Time (s)", previewTimeSec, 0f, selectedClip.length);
                    if (!Mathf.Approximately(newTime, previewTimeSec))
                    {
                        previewTimeSec = newTime;
                        SamplePreview();
                    }
                }
                EditorGUI.EndDisabledGroup();

                using (new EditorGUILayout.HorizontalScope())
                {
                    drawSceneGizmos = EditorGUILayout.ToggleLeft("Draw Gizmos", drawSceneGizmos, GUILayout.Width(100));
                    drawOrigins = EditorGUILayout.ToggleLeft("Draw Origins", drawOrigins, GUILayout.Width(110));
                    scaleGizmosFromShape = EditorGUILayout.ToggleLeft("Scale From Shape", scaleGizmosFromShape, GUILayout.Width(130));
                    if (!scaleGizmosFromShape)
                        gizmoSize = EditorGUILayout.Slider("Gizmo Size", gizmoSize, 0.01f, 0.2f);
                }
                gizmoVisualScale = EditorGUILayout.Slider("Visual Scale", gizmoVisualScale, 0.001f, 2f);

                EditorGUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Hitbox Detection", EditorStyles.boldLabel);

                    if (simpleMode)
                    {
                        float newSens = EditorGUILayout.Slider(new GUIContent("Sensitivity", "Right = stricter (fewer hitboxes)"), sensitivity, 0f, 1f);
                        if (!Mathf.Approximately(newSens, sensitivity))
                        {
                            sensitivity = newSens;
                            ApplySensitivity();
                        }

                        includeAllGroups = EditorGUILayout.ToggleLeft(new GUIContent("Include Minor Bones", "If enabled, bake hitboxes for all body groups."), includeAllGroups);

                        advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced Settings", true);
                        if (advancedFoldout)
                        {
                            autoThresholds = EditorGUILayout.ToggleLeft(new GUIContent("Auto Thresholds (per-clip)", "Compute thresholds from motion (percentiles)"), autoThresholds);
                            if (autoThresholds)
                            {
                                movementPercentile = EditorGUILayout.Slider(new GUIContent("Move Percentile", "Percentile of per-frame bone deltas used to activate"), movementPercentile, 0.50f, 0.999f);
                                deactivationPercentile = EditorGUILayout.Slider(new GUIContent("Deact Percentile", "Percentile used to deactivate"), deactivationPercentile, 0.0f, 0.99f);
                                deactRatioOfMove = EditorGUILayout.Slider(new GUIContent("Deact Ratio of Move", "Clamp deactivation to a fraction of the move threshold"), deactRatioOfMove, 0.10f, 1.0f);
                            }
                            else
                            {
                                movementThreshold = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Movement Threshold", "Distance per-frame to activate"), movementThreshold), 0f, 10f);
                                deactivationThreshold = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Deactivation Threshold", "Distance per-frame to deactivate"), deactivationThreshold), 0f, 10f);
                            }

                            useLocalDeltas = EditorGUILayout.ToggleLeft(new GUIContent("Use Local-Space Deltas", "Measure per-frame movement in local space relative to parent."), useLocalDeltas);
                            smoothWindow = EditorGUILayout.IntSlider(new GUIContent("Smoothing Window (frames)", "EMA smoothing (1 = off)."), Mathf.Clamp(smoothWindow, 1, 10), 1, 10);
                            minWindowFrames = EditorGUILayout.IntSlider(new GUIContent("Min Window Frames", "Discard activation windows shorter than this."), Mathf.Clamp(minWindowFrames, 1, 60), 1, 60);
                            minGapFrames = EditorGUILayout.IntSlider(new GUIContent("Min Gap Frames", "Frames to wait after deactivation before reactivating."), Mathf.Clamp(minGapFrames, 0, 60), 0, 60);
                        }
                    }
                    else
                    {
                        autoThresholds = EditorGUILayout.ToggleLeft(new GUIContent("Auto Thresholds (per-clip)", "Compute thresholds from motion (percentiles)"), autoThresholds);
                        if (autoThresholds)
                        {
                            movementPercentile = EditorGUILayout.Slider(new GUIContent("Move Percentile", "Percentile of per-frame bone deltas used to activate"), movementPercentile, 0.50f, 0.999f);
                            deactivationPercentile = EditorGUILayout.Slider(new GUIContent("Deact Percentile", "Percentile used to deactivate"), deactivationPercentile, 0.0f, 0.99f);
                            deactRatioOfMove = EditorGUILayout.Slider(new GUIContent("Deact Ratio of Move", "Clamp deactivation to a fraction of the move threshold"), deactRatioOfMove, 0.10f, 1.0f);
                            EditorGUILayout.HelpBox("Tip: Increase percentiles/ratio to reduce false activations.", MessageType.None);
                        }
                        else
                        {
                            movementThreshold = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Movement Threshold", "Distance per-frame to activate"), movementThreshold), 0f, 10f);
                            deactivationThreshold = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Deactivation Threshold", "Distance per-frame to deactivate"), deactivationThreshold), 0f, 10f);
                        }
                        includeAllGroups = EditorGUILayout.ToggleLeft(new GUIContent("Include Minor Bones", "If enabled, bake hitboxes for all body groups."), includeAllGroups);
                        useLocalDeltas = EditorGUILayout.ToggleLeft(new GUIContent("Use Local-Space Deltas", "Measure per-frame movement in local space relative to parent."), useLocalDeltas);
                        smoothWindow = EditorGUILayout.IntSlider(new GUIContent("Smoothing Window (frames)", "EMA smoothing (1 = off)."), Mathf.Clamp(smoothWindow, 1, 10), 1, 10);
                        minWindowFrames = EditorGUILayout.IntSlider(new GUIContent("Min Window Frames", "Discard activation windows shorter than this."), Mathf.Clamp(minWindowFrames, 1, 60), 1, 60);
                        minGapFrames = EditorGUILayout.IntSlider(new GUIContent("Min Gap Frames", "Frames to wait after deactivation before reactivating."), Mathf.Clamp(minGapFrames, 0, 60), 0, 60);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginDisabledGroup(selectedClip == null || targetModel == null || animator == null);
                        if (GUILayout.Button(new GUIContent("Rebake Clip", "Rebake the current clip using the settings above"), GUILayout.Height(22)))
                        {
                            BakeFromMotion();
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                }

                EditorGUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    // Live Threshold Preview (rebuilds preview keys without saving)
using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
{
    EditorGUILayout.LabelField("Live Threshold Preview", EditorStyles.boldLabel);
    bool newLive = EditorGUILayout.ToggleLeft(new GUIContent("Enable Live Preview", "Recompute windows from thresholds and draw them in SceneView without saving."), liveThresholdPreview);
    if (newLive != liveThresholdPreview)
    {
        liveThresholdPreview = newLive;
        if (liveThresholdPreview) MaybeRebuildLivePreview(true);
        Repaint();
        SceneView.RepaintAll();
    }

    EditorGUI.BeginDisabledGroup(!(liveThresholdPreview && selectedClip != null && targetModel != null && animator != null));
    if (liveThresholdPreview)
    {
        EditorGUILayout.LabelField($"Preview windows: {previewKeys.Count}");
        EditorGUILayout.LabelField($"Effective thresholds -> Move: {lastComputedMove:F4}  Deact: {lastComputedDeact:F4}");
        if (!(selectedClip != null && targetModel != null && animator != null))
            EditorGUILayout.HelpBox("Assign Target Model + Animator and select a Clip.", MessageType.Info);
    }
    EditorGUI.EndDisabledGroup();
}
// Auto rebuild if inputs changed
MaybeRebuildLivePreview(false);

EditorGUILayout.LabelField("Generation (Prefabs + Events)", EditorStyles.boldLabel);
                    generationHitboxPrefab = (GameObject)EditorGUILayout.ObjectField("Hitbox Prefab", generationHitboxPrefab, typeof(GameObject), false);
                    addAnimationEvents = EditorGUILayout.ToggleLeft("Add Animation Events", addAnimationEvents);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginDisabledGroup(animator == null || data == null || selectedClip == null || generationHitboxPrefab == null);
                        if (GUILayout.Button("Generate Prefab Hitboxes + Events", GUILayout.Height(24)))
                        {
                            GeneratePrefabsAndEventsFromData();
                        }
                        EditorGUI.EndDisabledGroup();

                        EditorGUI.BeginDisabledGroup(targetModel == null);
                        if (GUILayout.Button("Clear Prefab Hitboxes", GUILayout.Width(180), GUILayout.Height(24)))
                        {
                            ClearAllHitboxesInHierarchy();
                        }
                        EditorGUI.EndDisabledGroup();
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginDisabledGroup(targetModel == null || data == null);
                        if (GUILayout.Button("Attach Runtime Preview Component", GUILayout.Height(22)))
                        {
                            AttachRuntimePreviewComponent();
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                }
            }
        }
    }

    private void DrawKeysPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(320), GUILayout.MaxWidth(520)))
        {
            EditorGUILayout.LabelField("Hitbox Keys", EditorStyles.boldLabel);
            if (data == null)
            {
                EditorGUILayout.HelpBox("No HitboxClipData loaded. Load or Bake first.", MessageType.Info);
                return;
            }

            // Filters
            using (new EditorGUILayout.HorizontalScope())
            {
                groupByBodyPart = EditorGUILayout.ToggleLeft("Group by Body Part", groupByBodyPart, GUILayout.Width(150));
                var groups = new List<string> { "All" };
                groups.AddRange(data.keys.Select(k => string.IsNullOrEmpty(k.bodyGroup) ? "Other" : k.bodyGroup).Distinct().OrderBy(g => g));
                int sel = Mathf.Max(0, groups.IndexOf(filterGroup));
                sel = EditorGUILayout.Popup("Filter", sel, groups.ToArray());
                filterGroup = groups[Mathf.Clamp(sel, 0, groups.Count - 1)];
                GUILayout.FlexibleSpace();
            }

            // Quick add/delete
            using (new EditorGUILayout.HorizontalScope())
            {
                currentBonePathInput = EditorGUILayout.TextField("Current Bone Path", currentBonePathInput);
                if (GUILayout.Button("Set From Selection", GUILayout.Width(140)))
                {
                    if (animator == null) Debug.LogWarning("Animator not set; cannot compute bone path.");
                    var tr = Selection.activeTransform;
                    if (tr != null && animator != null)
                    {
                        currentBonePathInput = HitboxBakeUtil.GetTransformPath(animator.transform, tr);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Key at Preview", GUILayout.Width(160)))
                {
                    AddKeyAtPreview();
                }
                if (selectedKeyIndex >= 0 && selectedKeyIndex < data.keys.Count)
                {
                    if (GUILayout.Button("Delete Selected", GUILayout.Width(140)))
                    {
                        data.keys.RemoveAt(selectedKeyIndex);
                        selectedKeyIndex = -1;
                        MarkDirty();
                    }
                }
                GUILayout.FlexibleSpace();
            }

            // Bulk Resize (groups + global)
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Bulk Resize", EditorStyles.boldLabel);

                // Global scale row
                using (new EditorGUILayout.HorizontalScope())
                {
                    globalScaleUI = EditorGUILayout.FloatField(new GUIContent("Global x", "Multiply sizes (radius/box) for all keys"), Mathf.Clamp(globalScaleUI, 0.01f, 100f), GUILayout.Width(220));
                    if (GUILayout.Button("Apply to All", GUILayout.Width(120)))
                    {
                        ApplyScaleToAll(globalScaleUI);
                    }
                    GUILayout.FlexibleSpace();
                }

                // Per-group scale rows
                var presentGroups = data.keys.Select(k => string.IsNullOrEmpty(k.bodyGroup) ? "Other" : k.bodyGroup).Distinct().OrderBy(g => g).ToList();
                foreach (var g in presentGroups)
                {
                    if (!groupScaleUI.ContainsKey(g)) groupScaleUI[g] = 1f;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(g, GUILayout.Width(120));
                        groupScaleUI[g] = EditorGUILayout.FloatField("x", Mathf.Clamp(groupScaleUI[g], 0.01f, 100f), GUILayout.Width(120));
                        if (GUILayout.Button("Apply", GUILayout.Width(80)))
                        {
                            ApplyScaleToGroup(g, groupScaleUI[g]);
                        }
                        if (GUILayout.Button("Reset Defaults", GUILayout.Width(120)))
                        {
                            ResetGroupToDefaults(g);
                        }
                        GUILayout.FlexibleSpace();
                    }
                }
            }

            // List
            keysScroll = EditorGUILayout.BeginScrollView(keysScroll);
            IEnumerable<IGrouping<string, HitboxKey>> grouped;

            IEnumerable<HitboxKey> filtered = data.keys;
            if (filterGroup != "All")
                filtered = filtered.Where(k => (string.IsNullOrEmpty(k.bodyGroup) ? "Other" : k.bodyGroup) == filterGroup);

            if (groupByBodyPart)
            {
                grouped = filtered.GroupBy(k => string.IsNullOrEmpty(k.bodyGroup) ? "Other" : k.bodyGroup);
            }
            else
            {
                grouped = filtered.GroupBy(k => "All");
            }

            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                if (!groupFoldouts.ContainsKey(group.Key)) groupFoldouts[group.Key] = true;
                groupFoldouts[group.Key] = EditorGUILayout.Foldout(groupFoldouts[group.Key], $"{group.Key} ({group.Count()})", true);
                if (!groupFoldouts[group.Key]) continue;

                foreach (var key in group)
                {
                    int idx = data.keys.IndexOf(key);
                    DrawKeyRow(key, idx);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawKeyRow(HitboxKey key, int index)
    {
        var bg = (selectedKeyIndex == index) ? new GUIStyle(EditorStyles.helpBox) : EditorStyles.helpBox;
        using (new EditorGUILayout.VerticalScope(bg))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(selectedKeyIndex == index ? "Selected" : "Select", GUILayout.Width(80)))
                {
                    selectedKeyIndex = index;
                }
                EditorGUILayout.LabelField(key.hitboxId);
                bool newEnabled = EditorGUILayout.ToggleLeft("Enabled", key.enabled, GUILayout.Width(90));
                if (newEnabled != key.enabled) { key.enabled = newEnabled; MarkDirty(); }
                GUILayout.FlexibleSpace();
                key.bodyGroup = EditorGUILayout.TextField("Group", key.bodyGroup, GUILayout.Width(250));
            }

            // Simple row content
            if (simpleMode)
            {
                // Frame range
                float s = key.startFrame;
                float e = key.endFrame;
                EditorGUILayout.MinMaxSlider(new GUIContent("Frames"), ref s, ref e, 0, totalFrames > 0 ? totalFrames : 600);
                int ns = Mathf.RoundToInt(s);
                int ne = Mathf.RoundToInt(e);
                if (ns != key.startFrame || ne != key.endFrame) { key.startFrame = ns; key.endFrame = ne; MarkDirty(); }

                // Shape + size
                using (new EditorGUILayout.HorizontalScope())
                {
                    key.shape = (HitboxShape)EditorGUILayout.EnumPopup("Shape", key.shape);
                    if (key.shape == HitboxShape.Sphere)
                    {
                        float r = EditorGUILayout.Slider("Radius", key.radius, 0.02f, 1.0f);
                        if (!Mathf.Approximately(r, key.radius)) { key.radius = r; MarkDirty(); }
                    }
                    else
                    {
                        Vector3 bs = EditorGUILayout.Vector3Field("Box Size", key.boxSize);
                        if (bs != key.boxSize) { key.boxSize = bs; MarkDirty(); }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Go to Start", GUILayout.Width(120))) JumpToFrame(key.startFrame);
                    if (GUILayout.Button("Go to End", GUILayout.Width(120))) JumpToFrame(key.endFrame);
                    if (GUILayout.Button("Apply Pose From Preview", GUILayout.Width(180))) ApplyPoseFromPreview(key);
                    GUILayout.FlexibleSpace();
                }
            }
            else
            {
                // Advanced row content (full transforms)
                using (new EditorGUILayout.HorizontalScope())
                {
                    key.bonePath = EditorGUILayout.TextField("Bone Path", key.bonePath);
                    if (GUILayout.Button("Use Current", GUILayout.Width(110)))
                    {
                        if (!string.IsNullOrEmpty(currentBonePathInput))
                        {
                            key.bonePath = currentBonePathInput;
                            MarkDirty();
                        }
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    key.startFrame = EditorGUILayout.IntField("Start Frame", key.startFrame);
                    key.endFrame = EditorGUILayout.IntField("End Frame", key.endFrame);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    Vector3 lp = EditorGUILayout.Vector3Field("Local Pos", key.localPosition);
                    if (lp != key.localPosition) { key.localPosition = lp; MarkDirty(); }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    Vector4 lr = new Vector4(key.localRotation.x, key.localRotation.y, key.localRotation.z, key.localRotation.w);
                    Vector4 newLr = EditorGUILayout.Vector4Field("Local Rot (xyzw)", lr);
                    if (newLr != lr)
                    {
                        key.localRotation = new Quaternion(newLr.x, newLr.y, newLr.z, newLr.w);
                        MarkDirty();
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    Vector3 ls = EditorGUILayout.Vector3Field("Local Scale", key.localScale);
                    if (ls != key.localScale) { key.localScale = ls; MarkDirty(); }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    key.shape = (HitboxShape)EditorGUILayout.EnumPopup("Shape", key.shape);
                    if (key.shape == HitboxShape.Sphere)
                    {
                        float r = EditorGUILayout.Slider("Radius", key.radius, 0.02f, 1.0f);
                        if (!Mathf.Approximately(r, key.radius)) { key.radius = r; MarkDirty(); }
                    }
                    else
                    {
                        Vector3 bs = EditorGUILayout.Vector3Field("Box Size", key.boxSize);
                        if (bs != key.boxSize) { key.boxSize = bs; MarkDirty(); }
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Go to Start", GUILayout.Width(120))) JumpToFrame(key.startFrame);
                    if (GUILayout.Button("Go to End", GUILayout.Width(120))) JumpToFrame(key.endFrame);
                    if (GUILayout.Button("Apply Pose From Preview", GUILayout.Width(180))) ApplyPoseFromPreview(key);
                    GUILayout.FlexibleSpace();
                }
            }
        }
    }

    private void SelectClip(AnimationClip clip)
    {
        selectedClip = clip;
        if (selectedClip != null)
        {
            fps = Mathf.Max(1f, selectedClip.frameRate);
            totalFrames = Mathf.CeilToInt(selectedClip.length * fps);
            previewTimeSec = 0f;
        }
    }

    private void DiscoverClipsFromAnimator()
    {
        discoveredClips.Clear();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            var arr = animator.runtimeAnimatorController.animationClips;
            if (arr != null) discoveredClips.AddRange(arr.Distinct());
        }
        discoveredClips = discoveredClips.Distinct().OrderBy(c => c.name).ToList();
    }

    private void DiscoverClipsInProject()
    {
        discoveredClips.Clear();
        var guids = AssetDatabase.FindAssets("t:AnimationClip");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) discoveredClips.Add(clip);
        }
        discoveredClips = discoveredClips.Distinct().OrderBy(c => c.name).ToList();
    }

    // Batch bake all discovered clips for the assigned Animator
    private void BatchBakeFromAnimator()
    {
        if (animator == null || targetModel == null)
        {
            EditorUtility.DisplayDialog("Hitbox Workbench", "Assign Target Model with Animator first.", "OK");
            return;
        }

        // If nothing listed yet, pull from controller
        if (discoveredClips.Count == 0 && animator.runtimeAnimatorController != null)
        {
            discoveredClips.AddRange(animator.runtimeAnimatorController.animationClips);
            discoveredClips = discoveredClips.Distinct().OrderBy(c => c.name).ToList();
        }

        if (discoveredClips.Count == 0)
        {
            EditorUtility.DisplayDialog("Hitbox Workbench", "No clips discovered to bake.", "OK");
            return;
        }

        NormalizeSaveFolder();

        int baked = 0;
        foreach (var clip in discoveredClips)
        {
            if (clip == null) continue;
            try
            {
                BakeClipSilently(clip);
                baked++;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Batch bake failed for clip '{clip?.name}': {ex.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Hitbox Workbench", $"Batch baked {baked} clips into '{saveFolder}'.", "OK");
    }

    // Silent baker that mirrors BakeFromMotion but without popups per-clip
    private void BakeClipSilently(AnimationClip clipToBake)
    {
        if (clipToBake == null || targetModel == null || animator == null)
            throw new InvalidOperationException("Missing Target Model / Animator / Clip.");

        // Compute thresholds (same approach as BakeFromMotion)
        float clipFps = Mathf.Max(1f, clipToBake.frameRate);
        int frames = Mathf.CeilToInt(clipToBake.length * clipFps);
        var bones = animator.GetComponentsInChildren<Transform>(true);

        var deltas = new List<float>(bones.Length * frames);
        var lastPos = new Dictionary<Transform, Vector3>(bones.Length);
        var smoothByBone = new Dictionary<Transform, float>(bones.Length);
        foreach (var b in bones)
        {
            lastPos[b] = useLocalDeltas ? b.localPosition : b.position;
            smoothByBone[b] = 0f;
        }

        for (int f = 0; f <= frames; f++)
        {
            float t = Mathf.Min(clipToBake.length, f / clipFps);
            clipToBake.SampleAnimation(targetModel, t);
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                if (bone == animator.transform) continue;
                if (HitboxBakeUtil.ShouldSkipBone(bone.name)) continue;
                var prev = lastPos[bone];
                var cur = useLocalDeltas ? bone.localPosition : bone.position;
                float d = (cur - prev).magnitude;
                lastPos[bone] = cur;
                if (smoothWindow > 1)
                {
                    float alpha = 2f / (smoothWindow + 1f);
                    float sm = Mathf.Lerp(smoothByBone[bone], d, alpha);
                    smoothByBone[bone] = sm;
                    deltas.Add(sm);
                }
                else
                {
                    deltas.Add(d);
                }
            }
        }

        float move, deact;
        if (autoThresholds)
        {
            if (deltas.Count > 0)
            {
                move = HitboxBakeUtil.Percentile(deltas, movementPercentile);
                deact = Mathf.Min(move * deactRatioOfMove, HitboxBakeUtil.Percentile(deltas, deactivationPercentile));
                if (move < 1e-5f) move = 0.05f;
                if (deact < 1e-6f) deact = Mathf.Min(0.02f, move * deactRatioOfMove);
            }
            else
            {
                move = 0.05f;
                deact = 0.02f;
            }
        }
        else
        {
            move = Mathf.Max(0f, movementThreshold);
            deact = Mathf.Max(0f, deactivationThreshold);
        }
 
        SanitizeThresholds(ref move, ref deact, deltas);
 
         // Create or load asset for this clip
        var safeClipName = clipToBake.name.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
        var owner = animator != null ? animator.gameObject.name : "Character";
        var assetPath = $"{saveFolder}/{owner}_{safeClipName}_HitboxClipData.asset";
        var asset = AssetDatabase.LoadAssetAtPath<HitboxClipData>(assetPath);
        if (asset == null)
        {
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            asset = ScriptableObject.CreateInstance<HitboxClipData>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        asset.clip = clipToBake;
        asset.rigRootName = owner;
        asset.frameRate = clipFps;
        asset.keys.Clear();

        var lastPosWin = new Dictionary<Transform, Vector3>(bones.Length);
        var smoothByBoneWin = new Dictionary<Transform, float>(bones.Length);
        var activeByBone = new Dictionary<Transform, bool>(bones.Length);
        var currentKeyIdByBone = new Dictionary<Transform, string>(bones.Length);
        var startFrameByBone = new Dictionary<Transform, int>(bones.Length);
        var cooldownUntilFrameByBone = new Dictionary<Transform, int>(bones.Length);
        foreach (var b in bones)
        {
            lastPosWin[b] = useLocalDeltas ? b.localPosition : b.position;
            smoothByBoneWin[b] = 0f;
            activeByBone[b] = false;
            currentKeyIdByBone[b] = null;
            startFrameByBone[b] = 0;
            cooldownUntilFrameByBone[b] = 0;
        }

        for (int frame = 0; frame <= frames; frame++)
        {
            float t = Mathf.Min(clipToBake.length, frame / clipFps);
            clipToBake.SampleAnimation(targetModel, t);

            foreach (var bone in bones)
            {
                if (bone == null) continue;
                if (bone == animator.transform) continue;
                if (HitboxBakeUtil.ShouldSkipBone(bone.name)) continue;

                var prev = lastPosWin[bone];
                var cur = useLocalDeltas ? bone.localPosition : bone.position;
                float m = (cur - prev).magnitude;
                lastPosWin[bone] = cur;
                if (smoothWindow > 1)
                {
                    float alpha = 2f / (smoothWindow + 1f);
                    m = Mathf.Lerp(smoothByBoneWin[bone], m, alpha);
                    smoothByBoneWin[bone] = m;
                }

                if (!activeByBone[bone] && frame >= cooldownUntilFrameByBone[bone] && m > move)
                {
                    var hitboxBone = bone.parent != null ? bone.parent : bone;
                    var group = HitboxBakeUtil.BodyGroupFor(hitboxBone.name);

                    // Reduce noise: only generate for major end-effectors unless including all
                    if (!includeAllGroups && !IsMajorGroup(group))
                    {
                        // skip window creation for non-major groups
                    }
                    else
                    {
                        activeByBone[bone] = true;
                        startFrameByBone[bone] = frame;
                        var id = $"{bone.parent?.name ?? bone.name}_{clipToBake.name}_{frame}";
                        currentKeyIdByBone[bone] = id;

                        var defaultRadius = DefaultRadiusForGroup(group);
                        var key = new HitboxKey
                        {
                            hitboxId = id,
                            bonePath = HitboxBakeUtil.GetTransformPath(animator.transform, hitboxBone),
                            startFrame = frame,
                            endFrame = frame,
                            localPosition = (bone.parent != null ? bone.parent.InverseTransformPoint(bone.position) : bone.localPosition),
                            localRotation = (bone.parent != null ? Quaternion.Inverse(bone.parent.rotation) * bone.rotation : bone.localRotation),
                            localScale = (bone.parent != null ? Vector3.Scale(bone.localScale, Vector3.one) : bone.localScale),
                            bodyGroup = group,
                            shape = HitboxShape.Sphere,
                            radius = defaultRadius,
                            boxSize = new Vector3(defaultRadius, defaultRadius, defaultRadius)
                        };
                        asset.keys.Add(key);
                    }
                }
                else if (activeByBone[bone] && m < deact)
                {
                    activeByBone[bone] = false;
                    var id = currentKeyIdByBone[bone];
                    int start = startFrameByBone[bone];
                    int duration = frame - start;
                    if (duration < Mathf.Max(1, minWindowFrames))
                    {
                        // Remove too-short window
                        for (int i = asset.keys.Count - 1; i >= 0; i--)
                        {
                            if (asset.keys[i].hitboxId == id)
                            {
                                asset.keys.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int i = asset.keys.Count - 1; i >= 0; i--)
                        {
                            if (asset.keys[i].hitboxId == id)
                            {
                                asset.keys[i].endFrame = frame;
                                break;
                            }
                        }
                    }
                    currentKeyIdByBone[bone] = null;
                    cooldownUntilFrameByBone[bone] = frame + Mathf.Max(0, minGapFrames);
                }
            }
        }

         // Fallback if no windows baked: relax thresholds and include all groups
         if (asset.keys.Count == 0)
         {
             Debug.LogWarning("[Hitbox Workbench] Silent bake produced 0 windows. Retrying with relaxed thresholds.");
             float fbMove = deltas.Count > 0 ? Mathf.Max(1e-4f, HitboxBakeUtil.Percentile(deltas, 0.85f)) : 0.02f;
             float fbDeact = deltas.Count > 0 ? Mathf.Min(fbMove * 0.4f, HitboxBakeUtil.Percentile(deltas, 0.40f)) : 0.01f;
             SanitizeThresholds(ref fbMove, ref fbDeact, deltas);
        
             // Re-run build ignoring IsMajorGroup filter, with smoothing + min window
             asset.keys.Clear();
             var lastPos2 = new Dictionary<Transform, Vector3>(bones.Length);
             var smooth2 = new Dictionary<Transform, float>(bones.Length);
             var active2 = new Dictionary<Transform, bool>(bones.Length);
             var currentId2 = new Dictionary<Transform, string>(bones.Length);
             var start2 = new Dictionary<Transform, int>(bones.Length);
             foreach (var b in bones)
             {
                 lastPos2[b] = useLocalDeltas ? b.localPosition : b.position;
                 smooth2[b] = 0f;
                 active2[b] = false;
                 currentId2[b] = null;
                 start2[b] = 0;
             }
        
             for (int frame2 = 0; frame2 <= frames; frame2++)
             {
                 float t2 = Mathf.Min(clipToBake.length, frame2 / clipFps);
                 clipToBake.SampleAnimation(targetModel, t2);
        
                 foreach (var bone in bones)
                 {
                     if (bone == null) continue;
                     if (bone == animator.transform) continue;
                     if (HitboxBakeUtil.ShouldSkipBone(bone.name)) continue;
        
                     var pv = lastPos2[bone];
                     var cu = useLocalDeltas ? bone.localPosition : bone.position;
                     float m2 = (cu - pv).magnitude;
                     lastPos2[bone] = cu;
                     if (smoothWindow > 1)
                     {
                         float alpha2 = 2f / (smoothWindow + 1f);
                         m2 = Mathf.Lerp(smooth2[bone], m2, alpha2);
                         smooth2[bone] = m2;
                     }
        
                     if (!active2[bone] && m2 > fbMove)
                     {
                         var hitboxBone = bone.parent != null ? bone.parent : bone;
                         var group = HitboxBakeUtil.BodyGroupFor(hitboxBone.name);
        
                         active2[bone] = true;
                         start2[bone] = frame2;
                         var id2 = $"{bone.parent?.name ?? bone.name}_{clipToBake.name}_{frame2}";
                         currentId2[bone] = id2;
        
                         var defaultRadius = DefaultRadiusForGroup(group);
                         var key2 = new HitboxKey
                         {
                             hitboxId = id2,
                             bonePath = HitboxBakeUtil.GetTransformPath(animator.transform, hitboxBone),
                             startFrame = frame2,
                             endFrame = frame2,
                             localPosition = (bone.parent != null ? bone.parent.InverseTransformPoint(bone.position) : bone.localPosition),
                             localRotation = (bone.parent != null ? Quaternion.Inverse(bone.parent.rotation) * bone.rotation : bone.localRotation),
                             localScale = (bone.parent != null ? Vector3.Scale(bone.localScale, Vector3.one) : bone.localScale),
                             bodyGroup = group,
                             shape = HitboxShape.Sphere,
                             radius = defaultRadius,
                             boxSize = new Vector3(defaultRadius, defaultRadius, defaultRadius)
                         };
                         asset.keys.Add(key2);
                     }
                     else if (active2[bone] && m2 < fbDeact)
                     {
                         active2[bone] = false;
                         var id2 = currentId2[bone];
                         int dur2 = frame2 - start2[bone];
                         if (dur2 < Mathf.Max(1, minWindowFrames))
                         {
                             for (int i2 = asset.keys.Count - 1; i2 >= 0; i2--)
                             {
                                 if (asset.keys[i2].hitboxId == id2)
                                 {
                                     asset.keys.RemoveAt(i2);
                                     break;
                                 }
                             }
                         }
                         else
                         {
                             for (int i2 = asset.keys.Count - 1; i2 >= 0; i2--)
                             {
                                 if (asset.keys[i2].hitboxId == id2)
                                 {
                                     asset.keys[i2].endFrame = frame2;
                                     break;
                                 }
                             }
                         }
                         currentId2[bone] = null;
                     }
                 }
             }
         }
        
         // Close windows with no end
         for (int i = 0; i < asset.keys.Count; i++)
         {
             if (asset.keys[i].endFrame < asset.keys[i].startFrame)
                 asset.keys[i].endFrame = frames;
         }

        EditorUtility.SetDirty(asset);
    }

    private void LoadOrCreateDataForSelection()
    {
        if (selectedClip == null)
        {
            EditorUtility.DisplayDialog("Hitbox Workbench", "Select a clip first.", "OK");
            return;
        }

        NormalizeSaveFolder();

        var safeClipName = selectedClip.name.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
        var owner = animator != null ? animator.gameObject.name : "Character";
        var assetPath = $"{saveFolder}/{owner}_{safeClipName}_HitboxClipData.asset";

        data = AssetDatabase.LoadAssetAtPath<HitboxClipData>(assetPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<HitboxClipData>();
            data.clip = selectedClip;
            data.rigRootName = owner;
            data.frameRate = Mathf.Max(1f, selectedClip.frameRate);
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(data);
        }
    }

    private void SaveDataAsset()
    {
        if (data == null)
        {
            EditorUtility.DisplayDialog("Hitbox Workbench", "No data asset to save.", "OK");
            return;
        }
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(data);
    }

    private void BakeFromMotion()
    {
        if (selectedClip == null || targetModel == null || animator == null)
        {
            EditorUtility.DisplayDialog("Hitbox Workbench", "Assign Target Model with Animator and select a Clip.", "OK");
            return;
        }

        // Ensure data exists
        if (data == null) LoadOrCreateDataForSelection();
        if (data == null) return;

        // Use similar logic to baker (auto thresholds)
        float clipFps = Mathf.Max(1f, selectedClip.frameRate);
        int frames = Mathf.CeilToInt(selectedClip.length * clipFps);
        var bones = animator.GetComponentsInChildren<Transform>(true);

        var deltas = new List<float>(bones.Length * frames);
        var lastPos = new Dictionary<Transform, Vector3>(bones.Length);
        var smoothByBone = new Dictionary<Transform, float>(bones.Length);
        foreach (var b in bones)
        {
            lastPos[b] = useLocalDeltas ? b.localPosition : b.position;
            smoothByBone[b] = 0f;
        }

        for (int f = 0; f <= frames; f++)
        {
            float t = Mathf.Min(selectedClip.length, f / clipFps);
            selectedClip.SampleAnimation(targetModel, t);
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                if (bone == animator.transform) continue;
                if (HitboxBakeUtil.ShouldSkipBone(bone.name)) continue;
                var prev = lastPos[bone];
                var cur = useLocalDeltas ? bone.localPosition : bone.position;
                float d = (cur - prev).magnitude;
                lastPos[bone] = cur;
                if (smoothWindow > 1)
                {
                    float alpha = 2f / (smoothWindow + 1f);
                    float sm = Mathf.Lerp(smoothByBone[bone], d, alpha);
                    smoothByBone[bone] = sm;
                    deltas.Add(sm);
                }
                else
                {
                    deltas.Add(d);
                }
            }
        }

        float move, deact;
        if (autoThresholds)
        {
            if (deltas.Count > 0)
            {
                move = HitboxBakeUtil.Percentile(deltas, movementPercentile);
                deact = Mathf.Min(move * deactRatioOfMove, HitboxBakeUtil.Percentile(deltas, deactivationPercentile));
                if (move < 1e-5f) move = 0.05f;
                if (deact < 1e-6f) deact = Mathf.Min(0.02f, move * deactRatioOfMove);
            }
            else
            {
                move = 0.05f;
                deact = 0.02f;
            }
        }
        else
        {
            move = Mathf.Max(0f, movementThreshold);
            deact = Mathf.Max(0f, deactivationThreshold);
        }
 
        SanitizeThresholds(ref move, ref deact, deltas);
 
         var lastPosWin = new Dictionary<Transform, Vector3>(bones.Length);
        var smoothByBoneWin = new Dictionary<Transform, float>(bones.Length);
        var activeByBone = new Dictionary<Transform, bool>(bones.Length);
        var currentKeyIdByBone = new Dictionary<Transform, string>(bones.Length);
        var startFrameByBone = new Dictionary<Transform, int>(bones.Length);
        var cooldownUntilFrameByBone = new Dictionary<Transform, int>(bones.Length);
        foreach (var b in bones)
        {
            lastPosWin[b] = useLocalDeltas ? b.localPosition : b.position;
            smoothByBoneWin[b] = 0f;
            activeByBone[b] = false;
            currentKeyIdByBone[b] = null;
            startFrameByBone[b] = 0;
            cooldownUntilFrameByBone[b] = 0;
        }

        data.keys.Clear();
        for (int frame = 0; frame <= frames; frame++)
        {
            float t = Mathf.Min(selectedClip.length, frame / clipFps);
            selectedClip.SampleAnimation(targetModel, t);

            foreach (var bone in bones)
            {
                if (bone == null) continue;
                if (bone == animator.transform) continue;
                if (HitboxBakeUtil.ShouldSkipBone(bone.name)) continue;

                var prev = lastPosWin[bone];
                var cur = useLocalDeltas ? bone.localPosition : bone.position;
                float m = (cur - prev).magnitude;
                lastPosWin[bone] = cur;
                if (smoothWindow > 1)
                {
                    float alpha = 2f / (smoothWindow + 1f);
                    m = Mathf.Lerp(smoothByBoneWin[bone], m, alpha);
                    smoothByBoneWin[bone] = m;
                }

                if (!activeByBone[bone] && frame >= cooldownUntilFrameByBone[bone] && m > move)
                {
                    var hitboxBone = bone.parent != null ? bone.parent : bone;
                    var group = HitboxBakeUtil.BodyGroupFor(hitboxBone.name);

                    // Reduce noise: only generate for major groups unless including all
                    if (!includeAllGroups && !IsMajorGroup(group))
                    {
                        // Skip creating/activating a window for non-major groups
                    }
                    else
                    {
                        activeByBone[bone] = true;
                        startFrameByBone[bone] = frame;
                        var id = $"{bone.parent?.name ?? bone.name}_{selectedClip.name}_{frame}";
                        currentKeyIdByBone[bone] = id;

                        var defaultRadius = DefaultRadiusForGroup(group);
                        var key = new HitboxKey
                        {
                            hitboxId = id,
                            bonePath = HitboxBakeUtil.GetTransformPath(animator.transform, hitboxBone),
                            startFrame = frame,
                            endFrame = frame,
                            localPosition = (bone.parent != null ? bone.parent.InverseTransformPoint(bone.position) : bone.localPosition),
                            localRotation = (bone.parent != null ? Quaternion.Inverse(bone.parent.rotation) * bone.rotation : bone.localRotation),
                            localScale = (bone.parent != null ? Vector3.Scale(bone.localScale, Vector3.one) : bone.localScale),
                            bodyGroup = group,
                            shape = HitboxShape.Sphere,
                            radius = defaultRadius,
                            boxSize = new Vector3(defaultRadius, defaultRadius, defaultRadius)
                        };
                        data.keys.Add(key);
                    }
                }
                else if (activeByBone[bone] && m < deact)
                {
                    activeByBone[bone] = false;
                    var id = currentKeyIdByBone[bone];
                    int start = startFrameByBone[bone];
                    int duration = frame - start;
                    if (duration < Mathf.Max(1, minWindowFrames))
                    {
                        for (int i = data.keys.Count - 1; i >= 0; i--)
                        {
                            if (data.keys[i].hitboxId == id)
                            {
                                data.keys.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int i = data.keys.Count - 1; i >= 0; i--)
                        {
                            if (data.keys[i].hitboxId == id)
                            {
                                data.keys[i].endFrame = frame;
                                break;
                            }
                        }
                    }
                    currentKeyIdByBone[bone] = null;
                    cooldownUntilFrameByBone[bone] = frame + Mathf.Max(0, minGapFrames);
                }
            }
        }

        for (int i = data.keys.Count - 1; i >= 0; i--)
        {
            if (data.keys[i].endFrame < data.keys[i].startFrame)
                data.keys[i].endFrame = frames;
            int dur = data.keys[i].endFrame - data.keys[i].startFrame;
            if (dur < Mathf.Max(1, minWindowFrames))
                data.keys.RemoveAt(i);
        }

        MarkDirty();
        SaveDataAsset();
        ShowNotification(new GUIContent($"Baked {data.keys.Count} windows. move={move:F4} deact={deact:F4}"));
        Debug.Log($"[Hitbox Workbench] Baked {data.keys.Count} windows. move={move:F4} deact={deact:F4}");

        fps = clipFps;
        totalFrames = frames;
        previewTimeSec = 0f;
        Repaint();
        SceneView.RepaintAll();
    }

    private void TogglePlay()
    {
        if (!previewMode || selectedClip == null || targetModel == null) return;

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        isPlaying = !isPlaying;
        lastEditorTime = EditorApplication.timeSinceStartup;
        if (!isPlaying)
        {
            SamplePreview();
        }
    }

    private void SamplePreview()
    {
        if (!previewMode || selectedClip == null || targetModel == null) return;

        if (!AnimationMode.InAnimationMode())
            AnimationMode.StartAnimationMode();

        AnimationMode.SampleAnimationClip(targetModel, selectedClip, previewTimeSec);
        EditorUtility.SetDirty(targetModel);
        SceneView.RepaintAll();
    }

    private void JumpToFrame(int frame)
    {
        if (selectedClip == null) return;
        float clipFps = Mathf.Max(1f, selectedClip.frameRate);
        previewTimeSec = Mathf.Clamp(frame / clipFps, 0f, selectedClip.length);
        SamplePreview();
    }

    private void ApplyPoseFromPreview(HitboxKey key)
    {
        if (animator == null || string.IsNullOrEmpty(key.bonePath))
        {
            Debug.LogWarning("Cannot apply pose: missing Animator or bonePath.");
            return;
        }

        var bone = animator.transform.Find(key.bonePath);
        if (bone == null)
        {
            Debug.LogWarning($"Bone path not found: {key.bonePath}");
            return;
        }

        key.localPosition = bone.localPosition;
        key.localRotation = bone.localRotation;
        key.localScale = bone.localScale;
        MarkDirty();
    }

    private void AddKeyAtPreview()
    {
        if (selectedClip == null)
        {
            EditorUtility.DisplayDialog("Hitbox Workbench", "Select a clip first.", "OK");
            return;
        }
        if (string.IsNullOrEmpty(currentBonePathInput))
        {
            EditorUtility.DisplayDialog("Hitbox Workbench", "Set Current Bone Path (use 'Set From Selection').", "OK");
            return;
        }

        int frame = Mathf.RoundToInt(previewTimeSec * Mathf.Max(1f, selectedClip.frameRate));
        string boneName = currentBonePathInput.Split('/').Last();
        string id = $"{boneName}_{selectedClip.name}_{frame}";

        var key = new HitboxKey
        {
            hitboxId = id,
            bonePath = currentBonePathInput,
            startFrame = frame,
            endFrame = frame + 1,
            localPosition = Vector3.zero,
            localRotation = Quaternion.identity,
            localScale = Vector3.one,
            bodyGroup = HitboxBakeUtil.BodyGroupFor(boneName),
            shape = HitboxShape.Sphere,
            radius = DefaultRadiusForGroup(HitboxBakeUtil.BodyGroupFor(boneName)),
            boxSize = Vector3.one * DefaultRadiusForGroup(HitboxBakeUtil.BodyGroupFor(boneName))
        };

        if (animator != null)
        {
            var bone = animator.transform.Find(key.bonePath);
            if (bone != null)
            {
                key.localPosition = bone.localPosition;
                key.localRotation = bone.localRotation;
                key.localScale = bone.localScale;
            }
        }

        data.keys.Add(key);
        selectedKeyIndex = data.keys.Count - 1;
        MarkDirty();
    }

    private void NormalizeSaveFolder()
    {
        var normalized = saveFolder.Replace("\\", "/").Trim('/');
        if (!normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            normalized = "Assets/" + normalized;

        var parts = normalized.Split('/');
        var current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
        saveFolder = normalized;
    }

    private void OnSceneGUI(SceneView view)
    {
        if (!drawSceneGizmos || !previewMode || selectedClip == null || animator == null)
            return;

        int frame = Mathf.RoundToInt(previewTimeSec * Mathf.Max(1f, data.frameRate <= 0 ? selectedClip.frameRate : data.frameRate));

        if (data != null)
        {
            foreach (var key in data.keys)
            {
                if (!key.enabled) continue;
                if (frame < key.startFrame || frame > key.endFrame) continue;
                var bone = animator.transform.Find(key.bonePath);
                if (bone == null) continue;

                var pos = bone.TransformPoint(key.localPosition);
                var rot = bone.rotation * key.localRotation;
                var scale = bone.lossyScale;

                bool isSelected = (selectedKeyIndex >= 0 && selectedKeyIndex < data.keys.Count && data.keys[selectedKeyIndex] == key);
                Handles.color = isSelected ? selectedGizmoColor : gizmoColor;

                if (key.shape == HitboxShape.Sphere)
                {
                    float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                    float r = (scaleGizmosFromShape ? (key.radius * maxScale) : gizmoSize) * gizmoVisualScale;
                    DrawWireSphere(pos, rot, r);
                }
                else
                {
                    Vector3 size = (scaleGizmosFromShape ? Vector3.Scale(key.boxSize, scale) : (Vector3.one * gizmoSize)) * gizmoVisualScale;
                    using (new Handles.DrawingScope(Matrix4x4.TRS(pos, rot, Vector3.one)))
                    {
                        Handles.DrawWireCube(Vector3.zero, size);
                    }
                }

                // Small origin dot for clarity
                if (drawOrigins)
                {
                    var prevCol = Handles.color;
                    Handles.color = originColor;
                    float dotSize = HandleUtility.GetHandleSize(pos) * 0.03f;
                    Handles.SphereHandleCap(0, pos, Quaternion.identity, dotSize, EventType.Repaint);
                    Handles.color = prevCol;
                }
            }
        }
// Draw live preview windows (not saved), if enabled
if (liveThresholdPreview && previewKeys != null && previewKeys.Count > 0)
{
    int frame2 = Mathf.RoundToInt(previewTimeSec * Mathf.Max(1f, selectedClip.frameRate));

    foreach (var key in previewKeys)
    {
        if (frame2 < key.startFrame || frame2 > key.endFrame) continue;
        var bone = animator.transform.Find(key.bonePath);
        if (bone == null) continue;

        var pos = bone.TransformPoint(key.localPosition);
        var rot = bone.rotation * key.localRotation;
        var scale = bone.lossyScale;

        var prevCol = Handles.color;
        Handles.color = previewGizmoColor;

        if (key.shape == HitboxShape.Sphere)
        {
            float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            float r = (scaleGizmosFromShape ? (key.radius * maxScale) : gizmoSize) * gizmoVisualScale;
            DrawWireSphere(pos, rot, r);
        }
        else
        {
            Vector3 size = (scaleGizmosFromShape ? Vector3.Scale(key.boxSize, scale) : (Vector3.one * gizmoSize)) * gizmoVisualScale;
            using (new Handles.DrawingScope(Matrix4x4.TRS(pos, rot, Vector3.one)))
            {
                Handles.DrawWireCube(Vector3.zero, size);
            }
        }

        if (drawOrigins)
        {
            Handles.color = originColor;
            float dotSize = HandleUtility.GetHandleSize(pos) * 0.03f;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, dotSize, EventType.Repaint);
        }

        Handles.color = prevCol;
    }
}
    }

    private static void DrawWireSphere(Vector3 center, Quaternion rotation, float radius)
    {
        var up = rotation * Vector3.up;
        var right = rotation * Vector3.right;
        var forward = rotation * Vector3.forward;
        Handles.DrawWireDisc(center, up, radius);
        Handles.DrawWireDisc(center, right, radius);
        Handles.DrawWireDisc(center, forward, radius);
    }

    private void MarkDirty()
    {
        if (data != null)
        {
            EditorUtility.SetDirty(data);
        }
        Repaint();
    }
private void MaybeRebuildLivePreview(bool force)
{
    if (!liveThresholdPreview)
    {
        previewKeys.Clear();
        return;
    }
    if (selectedClip == null || targetModel == null || animator == null)
        return;

    bool changed =
        force
        || lastAutoThresholds != autoThresholds
        || !Mathf.Approximately(lastMovementThreshold, movementThreshold)
        || !Mathf.Approximately(lastDeactivationThreshold, deactivationThreshold)
        || !Mathf.Approximately(lastMovementPercentile, movementPercentile)
        || !Mathf.Approximately(lastDeactivationPercentile, deactivationPercentile)
        || !Mathf.Approximately(lastDeactRatioOfMove, deactRatioOfMove)
        || lastUseLocalDeltas != useLocalDeltas
        || lastSmoothWindow != smoothWindow
        || lastMinWindowFrames != minWindowFrames
        || lastMinGapFrames != minGapFrames
        || lastSelectedClip != selectedClip
        || lastTargetModel != targetModel;

    if (!changed) return;

    lastAutoThresholds = autoThresholds;
    lastMovementThreshold = movementThreshold;
    lastDeactivationThreshold = deactivationThreshold;
    lastMovementPercentile = movementPercentile;
    lastDeactivationPercentile = deactivationPercentile;
    lastDeactRatioOfMove = deactRatioOfMove;
    lastSelectedClip = selectedClip;
    lastTargetModel = targetModel;
    lastUseLocalDeltas = useLocalDeltas;
    lastSmoothWindow = smoothWindow;
    lastMinWindowFrames = minWindowFrames;
    lastMinGapFrames = minGapFrames;
    lastUseLocalDeltas = useLocalDeltas;
    lastSmoothWindow = smoothWindow;
    lastMinWindowFrames = minWindowFrames;
    lastMinGapFrames = minGapFrames;

    float clipFps = Mathf.Max(1f, selectedClip.frameRate);
    int frames = Mathf.CeilToInt(selectedClip.length * clipFps);
    var bones = animator.GetComponentsInChildren<Transform>(true);

    // Pre-pass to compute auto thresholds if needed
    var deltas = new List<float>(bones.Length * frames);
    var lastPosPre = new Dictionary<Transform, Vector3>(bones.Length);
    var smoothPre = new Dictionary<Transform, float>(bones.Length);
    foreach (var b in bones)
    {
        lastPosPre[b] = useLocalDeltas ? b.localPosition : b.position;
        smoothPre[b] = 0f;
    }

    for (int f = 0; f <= frames; f++)
    {
        float t = Mathf.Min(selectedClip.length, f / clipFps);
        selectedClip.SampleAnimation(targetModel, t);
        foreach (var bone in bones)
        {
            if (bone == null) continue;
            if (bone == animator.transform) continue;
            if (HitboxBakeUtil.ShouldSkipBone(bone.name)) continue;
            var prev = lastPosPre[bone];
            var cur = useLocalDeltas ? bone.localPosition : bone.position;
            float d = (cur - prev).magnitude;
            lastPosPre[bone] = cur;
            if (smoothWindow > 1)
            {
                float alpha = 2f / (smoothWindow + 1f);
                float sm = Mathf.Lerp(smoothPre[bone], d, alpha);
                smoothPre[bone] = sm;
                deltas.Add(sm);
            }
            else
            {
                deltas.Add(d);
            }
        }
    }

    float move, deact;
    if (autoThresholds)
    {
        if (deltas.Count > 0)
        {
            move = HitboxBakeUtil.Percentile(deltas, movementPercentile);
            deact = Mathf.Min(move * deactRatioOfMove, HitboxBakeUtil.Percentile(deltas, deactivationPercentile));
            if (move < 1e-5f) move = 0.05f;
            if (deact < 1e-6f) deact = Mathf.Min(0.02f, move * deactRatioOfMove);
        }
        else
        {
            move = 0.05f;
            deact = 0.02f;
        }
    }
    else
    {
        move = Mathf.Max(0f, movementThreshold);
        deact = Mathf.Max(0f, deactivationThreshold);
    }
    SanitizeThresholds(ref move, ref deact, deltas);
    lastComputedMove = move;
    lastComputedDeact = deact;

    // Build preview windows
    previewKeys.Clear();
    var lastPosWin = new Dictionary<Transform, Vector3>(bones.Length);
    var smoothByBoneWin = new Dictionary<Transform, float>(bones.Length);
    var activeByBone = new Dictionary<Transform, bool>(bones.Length);
    var currentKeyIdByBone = new Dictionary<Transform, string>(bones.Length);
    var startFrameByBone = new Dictionary<Transform, int>(bones.Length);
    foreach (var b in bones)
    {
        lastPosWin[b] = useLocalDeltas ? b.localPosition : b.position;
        smoothByBoneWin[b] = 0f;
        activeByBone[b] = false;
        currentKeyIdByBone[b] = null;
        startFrameByBone[b] = 0;
    }

    for (int frame = 0; frame <= frames; frame++)
    {
        float t = Mathf.Min(selectedClip.length, frame / clipFps);
        selectedClip.SampleAnimation(targetModel, t);

        foreach (var bone in bones)
        {
            if (bone == null) continue;
            if (bone == animator.transform) continue;
            if (HitboxBakeUtil.ShouldSkipBone(bone.name)) continue;

            var prev = lastPosWin[bone];
            var cur = useLocalDeltas ? bone.localPosition : bone.position;
            float m = (cur - prev).magnitude;
            lastPosWin[bone] = cur;
            if (smoothWindow > 1)
            {
                float alpha = 2f / (smoothWindow + 1f);
                m = Mathf.Lerp(smoothByBoneWin[bone], m, alpha);
                smoothByBoneWin[bone] = m;
            }

            if (!activeByBone[bone] && m > move)
            {
                var hitboxBone = bone.parent != null ? bone.parent : bone;
                var group = HitboxBakeUtil.BodyGroupFor(hitboxBone.name);

                if (!includeAllGroups && !IsMajorGroup(group))
                {
                    // skip non-major groups to reduce noise
                }
                else
                {
                    activeByBone[bone] = true;
                    startFrameByBone[bone] = frame;
                    var id = $"{bone.parent?.name ?? bone.name}_{selectedClip.name}_{frame}_PREVIEW";
                    currentKeyIdByBone[bone] = id;

                    var defaultRadius = DefaultRadiusForGroup(group);
                    var key = new HitboxKey
                    {
                        hitboxId = id,
                        bonePath = HitboxBakeUtil.GetTransformPath(animator.transform, hitboxBone),
                        startFrame = frame,
                        endFrame = frame,
                        localPosition = (bone.parent != null ? bone.parent.InverseTransformPoint(bone.position) : bone.localPosition),
                        localRotation = (bone.parent != null ? Quaternion.Inverse(bone.parent.rotation) * bone.rotation : bone.localRotation),
                        localScale = (bone.parent != null ? Vector3.Scale(bone.localScale, Vector3.one) : bone.localScale),
                        bodyGroup = group,
                        shape = HitboxShape.Sphere,
                        radius = defaultRadius,
                        boxSize = new Vector3(defaultRadius, defaultRadius, defaultRadius)
                    };
                    previewKeys.Add(key);
                }
            }
            else if (activeByBone[bone] && m < deact)
            {
                activeByBone[bone] = false;
                var id = currentKeyIdByBone[bone];
                int start = startFrameByBone[bone];
                int duration = frame - start;
                if (duration < Mathf.Max(1, minWindowFrames))
                {
                    for (int i = previewKeys.Count - 1; i >= 0; i--)
                    {
                        if (previewKeys[i].hitboxId == id)
                        {
                            previewKeys.RemoveAt(i);
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = previewKeys.Count - 1; i >= 0; i--)
                    {
                        if (previewKeys[i].hitboxId == id)
                        {
                            previewKeys[i].endFrame = frame;
                            break;
                        }
                    }
                }
                currentKeyIdByBone[bone] = null;
            }
        }
    }

    for (int i = 0; i < previewKeys.Count; i++)
    {
        if (previewKeys[i].endFrame < previewKeys[i].startFrame)
            previewKeys[i].endFrame = frames;
    }

    Repaint();
    SceneView.RepaintAll();
}

    // Threshold helpers
    private static bool IsFinite(float v) { return !(float.IsNaN(v) || float.IsInfinity(v)); }
    private void SanitizeThresholds(ref float move, ref float deact, List<float> deltas)
    {
        // Clamp to sensible ranges and recover from invalid values
        if (!IsFinite(move) || move <= 0f || move > 10f)
        {
            if (deltas != null && deltas.Count > 0)
                move = Mathf.Max(0.05f, HitboxBakeUtil.Percentile(deltas, Mathf.Clamp01(movementPercentile)));
            else
                move = 0.05f;
        }

        if (!IsFinite(deact) || deact <= 0f || deact > 10f)
        {
            float ratio = Mathf.Clamp(deactRatioOfMove, 0.1f, 1.0f);
            float p = Mathf.Clamp01(deactivationPercentile);
            float perc = (deltas != null && deltas.Count > 0) ? HitboxBakeUtil.Percentile(deltas, p) : 0.02f;
            deact = Mathf.Min(move * ratio, perc);
        }

        // Ensure deactivation < activation
        float safeRatio = Mathf.Clamp(deactRatioOfMove, 0.1f, 0.99f);
        if (deact >= move)
            deact = Mathf.Max(1e-5f, move * safeRatio);
    }

    // Map single "Sensitivity" control to practical detection settings.
    // Higher sensitivity => stricter detection (fewer activations).
    private void ApplySensitivity()
    {
        float s = Mathf.Clamp01(sensitivity);

        // Prefer auto mode in Simple UX; manual fields remain untouched for Advanced mode.
        autoThresholds = true;

        // Threshold percentiles and hysteresis
        movementPercentile      = Mathf.Lerp(0.85f, 0.995f, s);  // raise to be stricter
        deactivationPercentile  = Mathf.Lerp(0.20f, 0.90f,  s);  // raise to shorten windows
        deactRatioOfMove        = Mathf.Lerp(0.45f, 0.85f,  s);  // closer to move at higher strictness

        // Smoothing and gating
        smoothWindow     = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 5f, s)), 1, 10);
        minWindowFrames  = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, 8f, s)), 1, 60);
        minGapFrames     = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0f, 8f, s)), 0, 60);

        // Favor local deltas in Simple mode to reduce root-motion noise
        useLocalDeltas = true;

        // Trigger a live preview rebuild next GUI pass
        Repaint();
    }

    // === Bulk resize helpers ===
    private void ApplyScaleToAll(float multiplier)
    {
        if (data == null) return;
        multiplier = Mathf.Clamp(multiplier, 0.01f, 100f);
        foreach (var k in data.keys)
        {
            if (k == null) continue;
            if (k.shape == HitboxShape.Sphere)
            {
                k.radius = Mathf.Max(0.01f, k.radius * multiplier);
            }
            else
            {
                var scaled = k.boxSize * multiplier;
                k.boxSize = new Vector3(Mathf.Max(0.01f, scaled.x), Mathf.Max(0.01f, scaled.y), Mathf.Max(0.01f, scaled.z));
            }
        }
        MarkDirty();
    }

    private void ApplyScaleToGroup(string group, float multiplier)
    {
        if (data == null) return;
        if (string.IsNullOrEmpty(group)) return;
        multiplier = Mathf.Clamp(multiplier, 0.01f, 100f);
        foreach (var k in data.keys)
        {
            if (k == null) continue;
            var g = string.IsNullOrEmpty(k.bodyGroup) ? "Other" : k.bodyGroup;
            if (g != group) continue;

            if (k.shape == HitboxShape.Sphere)
            {
                k.radius = Mathf.Max(0.01f, k.radius * multiplier);
            }
            else
            {
                var scaled = k.boxSize * multiplier;
                k.boxSize = new Vector3(Mathf.Max(0.01f, scaled.x), Mathf.Max(0.01f, scaled.y), Mathf.Max(0.01f, scaled.z));
            }
        }
        MarkDirty();
    }

    private void ResetGroupToDefaults(string group)
    {
        if (data == null) return;
        if (string.IsNullOrEmpty(group)) return;

        foreach (var k in data.keys)
        {
            if (k == null) continue;
            var g = string.IsNullOrEmpty(k.bodyGroup) ? "Other" : k.bodyGroup;
            if (g != group) continue;

            var defR = DefaultRadiusForGroup(group);
            if (k.shape == HitboxShape.Sphere)
            {
                k.radius = Mathf.Max(0.01f, defR);
            }
            else
            {
                var v = new Vector3(defR, defR, defR);
                k.boxSize = new Vector3(Mathf.Max(0.01f, v.x), Mathf.Max(0.01f, v.y), Mathf.Max(0.01f, v.z));
            }
        }
        MarkDirty();
    }

    private static bool IsMajorGroup(string group)
    {
        // Keep this intentionally tight to reduce spam:
        // Head and end effectors only.
        switch (group)
        {
            case "Head":
            case "Forearm.L":
            case "Forearm.R":
            case "Hand.L":
            case "Hand.R":
            case "Shin.L":
            case "Shin.R":
            case "Foot.L":
            case "Foot.R":
                return true;
            default:
                return false;
        }
    }

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

    // === Unified generation: prefabs + animation events ===
    private void GeneratePrefabsAndEventsFromData()
    {
        if (animator == null || targetModel == null || data == null || selectedClip == null || generationHitboxPrefab == null)
        {
            EditorUtility.DisplayDialog("Hitbox Workbench", "Assign Target Model, Animator, Clip, Data, and Hitbox Prefab.", "OK");
            return;
        }

        int created = 0;
        // Use baked framerate if available
        float fpsUsed = Mathf.Max(1f, data.frameRate <= 0 ? selectedClip.frameRate : data.frameRate);

        // Build a quick lookup for existing instances by id to avoid duplicates
        var existing = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        foreach (Transform t in targetModel.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t.name.StartsWith("hitbox_", StringComparison.OrdinalIgnoreCase))
            {
                existing[t.name.Substring("hitbox_".Length)] = t.gameObject;
            }
        }

        foreach (var key in data.keys)
        {
            if (key == null || !key.enabled || string.IsNullOrEmpty(key.bonePath)) continue;

            string id = key.hitboxId;
            // Ensure key has a stable id
            if (string.IsNullOrEmpty(id))
            {
                var boneName = key.bonePath.Split('/').Last();
                id = $"{boneName}_{selectedClip.name}_{key.startFrame}";
                key.hitboxId = id;
                EditorUtility.SetDirty(data);
            }

            // Ensure target bone exists
            var bone = animator.transform.Find(key.bonePath);
            if (bone == null) continue;

            // Create or reuse object
            GameObject go = null;
            if (!existing.TryGetValue(id, out go) || go == null)
            {
                // Instantiate under bone
                go = (GameObject)UnityEngine.Object.Instantiate(generationHitboxPrefab, bone, false);
                go.name = $"hitbox_{id}";
                created++;
            }

            // Apply local pose from key start
            go.transform.localPosition = key.localPosition;
            go.transform.localRotation = key.localRotation;

            // Size to match shape intent
            if (key.shape == HitboxShape.Sphere)
            {
                go.transform.localScale = Vector3.one * (key.radius * 2f);
            }
            else
            {
                go.transform.localScale = key.boxSize;
            }

            // Assign ID to component if available
            var hb = go.GetComponent<hitbox>();
            if (hb != null)
            {
                hb.HitboxID = id;
            }

            // Add animation events for activation windows
            if (addAnimationEvents)
            {
                float startTime = Mathf.Clamp01(key.startFrame / fpsUsed);
                float endTime = Mathf.Clamp01(key.endFrame / fpsUsed);
                AddAnimationEventIfMissing(selectedClip, startTime, "ActivateHitbox", id);
                AddAnimationEventIfMissing(selectedClip, endTime, "DeactivateHitbox", id);
            }
        }

        if (created > 0) EditorUtility.SetDirty(targetModel);
        EditorUtility.DisplayDialog("Hitbox Workbench", $"Generated/updated {created} prefab hitbox(es).{(addAnimationEvents ? " Animation events updated." : "")}", "OK");
    }

    private void ClearAllHitboxesInHierarchy()
    {
        if (targetModel == null)
        {
            EditorUtility.DisplayDialog("Hitbox Workbench", "Assign Target Model first.", "OK");
            return;
        }
        var list = new List<GameObject>();
        foreach (Transform t in targetModel.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name.ToLowerInvariant().Contains("hitbox"))
                list.Add(t.gameObject);
        }
        foreach (var go in list)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        EditorUtility.DisplayDialog("Hitbox Workbench", $"Cleared {list.Count} hitbox object(s).", "OK");
    }

    private void AttachRuntimePreviewComponent()
    {
        if (targetModel == null || data == null)
        {
            EditorUtility.DisplayDialog("Hitbox Workbench", "Assign Target Model and load a Data asset.", "OK");
            return;
        }

        var comp = targetModel.GetComponent<HitboxClipRuntimePreview>();
        if (comp == null) comp = targetModel.AddComponent<HitboxClipRuntimePreview>();
        comp.animator = targetModel.GetComponent<Animator>();
        comp.data = data;
        comp.hitboxPrefab = generationHitboxPrefab != null ? generationHitboxPrefab : comp.hitboxPrefab;
        comp.sizeFromShape = true;
        comp.drawGizmos = true;
        comp.drawOrigins = true;
        EditorUtility.SetDirty(targetModel);
        EditorUtility.DisplayDialog("Hitbox Workbench", "Attached and configured HitboxClipRuntimePreview on the target model.", "OK");
    }

    private static void AddAnimationEventIfMissing(AnimationClip clip, float time, string functionName, string stringParam)
    {
        if (clip == null) return;
        var events = AnimationUtility.GetAnimationEvents(clip);
        foreach (var e in events)
        {
            if (Mathf.Approximately(e.time, time) && e.functionName == functionName && e.stringParameter == stringParam)
            {
                return; // already present
            }
        }
        var list = new List<AnimationEvent>(events)
        {
            new AnimationEvent { time = time, functionName = functionName, stringParameter = stringParam }
        };
        AnimationUtility.SetAnimationEvents(clip, list.ToArray());
        EditorUtility.SetDirty(clip);
    }
}
#endif