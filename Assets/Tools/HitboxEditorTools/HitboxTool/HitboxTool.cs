using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class HitboxTool : MonoBehaviour
{
    public AnimationClip clip;
    public GameObject targetModel;
    public GameObject hitboxPrefab;
    public float playbackSpeed = 1.0f;
    public Color playbackIndicatorColor = Color.green;

    private Animator animator;
    private float currentTime = 0f;
    public bool isPlaying = false;
    private Dictionary<Transform, Vector3[]> boneTransforms;
    private Dictionary<Transform, float[]> boneMovements;
    private Dictionary<Transform, float> boneMovementTotals;
#if UNITY_EDITOR
    private double lastEditorTime = 0.0;
#endif

    void OnEnable()
    {
        if (targetModel != null)
        {
            animator = targetModel.GetComponent<Animator>();
            InitializeBoneTransforms();
        }
    #if UNITY_EDITOR
        EditorApplication.update += EditorUpdate;
    #endif
    }

    void OnDisable()
    {
    #if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
    #endif
    }

    private void OnValidate()
    {
        if (targetModel != null)
        {
            animator = targetModel.GetComponent<Animator>();
            InitializeBoneTransforms();
        }
        else
        {
            animator = null; // Reset or handle case where targetModel is null
        }
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (isPlaying)
        {
            double currentEditorTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentEditorTime - lastEditorTime);
            lastEditorTime = currentEditorTime;

            currentTime += playbackSpeed * deltaTime;
            int currentFrame = Mathf.FloorToInt(currentTime * clip.frameRate);

            if (currentTime > clip.length)
            {
                currentTime = 0f;
                isPlaying = false;
                Debug.Log("Animation playback completed.");
                return;
            }

            PlayAnimationInEditor();
            RepaintScene();

            // Activate or deactivate hitboxes based on the current frame
            foreach (var kvp in hitboxFrameRanges)
            {
                var hitbox = kvp.Key;
                var frameRange = kvp.Value;

                if (currentFrame >= frameRange.startFrame && currentFrame <= frameRange.endFrame)
                {
                    hitbox.SetActive(true);
                }
                else
                {
                    hitbox.SetActive(false);
                }
            }
        }
    }
#endif

    public void AnalyzeAnimation()
    {
        InitializeBoneTransforms();

        if (boneTransforms == null || boneTransforms.Count == 0)
        {
            
            Debug.LogError("BoneTransforms dictionary is not initialized or empty.");
            return;
        }

        Debug.Log($"Starting analysis of animation: {clip.name}");
        float totalTime = clip.length;
        int totalFrames = Mathf.CeilToInt(totalTime * clip.frameRate);

        foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
        {
            for (int frame = 0; frame < totalFrames; frame++)
            {
                float time = frame / clip.frameRate;
                clip.SampleAnimation(targetModel, time);
                AnalyzeFrame(frame, time, bone);

                if (frame > 0)
                {
                    Vector3 movement = bone.position - boneTransforms[bone][frame - 1];
                    float movementMagnitude = movement.magnitude;
                    boneMovements[bone][frame] = movementMagnitude;
                    boneMovementTotals[bone] += movementMagnitude;
                }
            }

            DetectAction(bone);
        }

        foreach (var kvp in boneMovementTotals)
        {
            if (kvp.Value > 0 && kvp.Value > 1) {
                Debug.Log($"Bone {kvp.Key.name} Total Movement: {kvp.Value}");
            }
        }

        Debug.Log($"Completed analysis of animation: {clip.name}");
    }

    private void DetectAction(Transform bone)
    {
        if (!boneMovements.ContainsKey(bone))
        {
            Debug.LogWarning($"Bone {bone.name} not found in movements dictionary.");
            return;
        }

        float[] movements = boneMovements[bone];
        float maxMovement = 0f;
        int maxMovementIndex = 0;

        // Accumulate movement up the hierarchy
        Transform currentBone = bone;
        while (currentBone != null)
        {
            if (boneMovements.ContainsKey(currentBone))
            {
                float[] currentMovements = boneMovements[currentBone];
                for (int i = 0; i < movements.Length; i++)
                {
                    movements[i] += currentMovements[i];
                }
            }
            currentBone = currentBone.parent;
        }

        // Detect maximum movement and its frame index
        for (int i = 0; i < movements.Length; i++)
        {
            if (movements[i] > maxMovement)
            {
                maxMovement = movements[i];
                maxMovementIndex = i;
            }
        }

        float burstThreshold = 1f; // Adjust this value based on your needs
        bool isBurstDetected = false;

        for (int i = 1; i < movements.Length; i++)
        {
            float movementRate = movements[i] - movements[i - 1];
            if (movementRate > burstThreshold)
            {
                isBurstDetected = true;
                break;
            }
        }

        if (isBurstDetected)
        {
            Debug.Log($"Detected rapid movement burst in bone {bone.name} at frame {maxMovementIndex}: {maxMovement}");

            // Generate a hitbox at the parent bone's position instead of the offset bone
            GenerateHitbox(bone.parent != null ? bone.parent : bone, maxMovementIndex);
        }
    }

    private void InitializeBoneTransforms()
    {
        if (targetModel == null || clip == null || animator == null)
        {
            Debug.LogError($"Initialization failed: {(targetModel ? "" : "targetModel" )}, {(clip ? "" : "clip")}, {(animator ? "" : "animator")} is missing.");
            return;
        }

        boneTransforms = new Dictionary<Transform, Vector3[]>();
        boneMovements = new Dictionary<Transform, float[]>();
        boneMovementTotals = new Dictionary<Transform, float>();

        foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
        {
            // Filter out any hitbox transforms
            if (bone.name.Contains("Hitbox") || bone.gameObject.name.Contains("Hitbox"))
            {
                continue;
            }

            boneTransforms[bone] = new Vector3[Mathf.CeilToInt(clip.length * clip.frameRate)];
            boneMovements[bone] = new float[Mathf.CeilToInt(clip.length * clip.frameRate)];
            boneMovementTotals[bone] = 0f;
        }

        Debug.Log("BoneTransforms dictionary initialized.");
    }

    private void AnalyzeFrame(int frameIndex, float time, Transform bone)
    {
        // Additional check to ensure boneTransforms contains the bone
        if (boneTransforms == null || !boneTransforms.ContainsKey(bone))
        {
            Debug.LogError($"BoneTransforms dictionary is not properly initialized or does not contain bone {bone.name}.");
            return;
        }

        boneTransforms[bone][frameIndex] = bone.position;
    }

    private Dictionary<GameObject, (int startFrame, int endFrame)> hitboxFrameRanges = new Dictionary<GameObject, (int startFrame, int endFrame)>();

    private Dictionary<Transform, GameObject> activeHitboxes = new Dictionary<Transform, GameObject>();

    [SerializeField] public float MovementThreshold = 2.0f; // Adjust this value to set the minimum movement to generate a hitbox
    
    [SerializeField] public float DeactivationThreshold = 1.2f;
    private const float MovementCooldown = 0.5f; // Time in seconds before allowing another hitbox for the same bone
    private Dictionary<Transform, double> lastHitboxTime = new Dictionary<Transform, double>();

    public void GenerateHitboxes()
    {
        if (hitboxPrefab == null)
        {
            Debug.LogError("Hitbox prefab is not assigned.");
            return;
        }

        InitializeBoneTransforms();

        if (boneTransforms == null || boneTransforms.Count == 0)
        {
            Debug.LogError("BoneTransforms dictionary is not initialized or empty.");
            return;
        }

        Debug.Log($"Starting hitbox generation for animation: {clip.name}");
        float totalTime = clip.length;
        int totalFrames = Mathf.CeilToInt(totalTime * clip.frameRate);

        // Track which bones have hitboxes
        HashSet<Transform> bonesWithHitboxes = new HashSet<Transform>();

        Transform[] animatorChildren = animator.GetComponentsInChildren<Transform>();

        foreach (Transform bone in animatorChildren)
        {
            Transform rootBone = animatorChildren[0];
            // Check if the bone name contains unwanted substrings
            if (bone.name.ToLower().Contains("unused") || bone.name.ToLower().Contains("adj")|| bone.name.ToLower().Contains("attached") || bone.name.ToLower().Contains("middle") || bone.name.ToLower().Contains("pinky")
                || bone.name.ToLower().Contains("finger") || bone.name.ToLower().Contains("index") || bone.name.ToLower().Contains("ring")
            )
            {
                // Debug.Log($"Skipping hitbox generation for bone: {bone.name}");
                continue; // Skip to the next bone
            }

            bool isHitboxActive = false;
            int startFrame = 0;

            for (int frame = 0; frame < totalFrames; frame++)
            {
                float time = frame / clip.frameRate;
                clip.SampleAnimation(targetModel, time);
                AnalyzeFrame(frame, time, bone);

                if (frame > 0)
                {
                    Vector3 movement = bone.position - boneTransforms[bone][frame - 1];
                    float movementMagnitude = movement.magnitude;
                    boneMovements[bone][frame] = movementMagnitude;
                    boneMovementTotals[bone] += movementMagnitude;

                    // Detect the start of significant movement
                    if (!isHitboxActive && movementMagnitude > MovementThreshold) // Adjust the threshold as needed
                    {
                        // Check if a hitbox already exists for this bone
                        if (!bonesWithHitboxes.Contains(bone))
                        {

                            startFrame = frame;
                            GameObject hitbox = GenerateHitbox(bone, frame);

                            // Store newly created hitbox for reference
                            allHitboxesInHierarchy.Add(hitbox);

                            hitboxFrameRanges[hitbox] = (startFrame, startFrame); // Initialize endFrame as startFrame for now
                            activeHitboxes[bone] = hitbox; // Track the active hitbox for this bone
                            bonesWithHitboxes.Add(bone); // Mark this bone as having a hitbox
                            isHitboxActive = true;

                            Debug.Log($"Hitbox created for {bone.name} at frame {frame}");
                            // Log movement details
                            Debug.Log($"{bone.name} - Frame: {frame}, Movement Magnitude: {movementMagnitude}");

                            var hbComp = hitbox.GetComponent<hitbox>();
                            if (hbComp != null)
                            {
                                SetAnimationEvent(time, "ActivateHitbox", hbComp.HitboxID);
                            }
                        }
                        else
                        {
                            Debug.Log($"Hitbox already exists for {bone.name}, skipping creation.");
                        }
                    }
                    // Detect the end of the movement
                    else if (isHitboxActive && movementMagnitude < DeactivationThreshold) // Define a threshold for when to deactivate
                    {
                        if (activeHitboxes.TryGetValue(bone, out GameObject hitbox))
                        {
                            hitboxFrameRanges[hitbox] = (hitboxFrameRanges[hitbox].startFrame, frame);
                            activeHitboxes.Remove(bone);
                            Debug.Log($"Hitbox for {bone.name} deactivated between frames {hitboxFrameRanges[hitbox].startFrame} and {frame}");

                            var hbComp = hitbox.GetComponent<hitbox>();
                            if (hbComp != null)
                            {
                                SetAnimationEvent(time, "DeactivateHitbox", hbComp.HitboxID);
                            }
                        }

                        isHitboxActive = false;
                    }
                }
            }
        }

        Debug.Log($"Completed hitbox generation for animation: {clip.name}");
    }

    private GameObject GenerateHitbox(Transform bone, int frameIndex)
    {
        // Check if the bone has a parent and if so, assign the hitbox to the parent bone
        Transform hitboxBone = bone.parent != null && bone.parent != this.transform ? bone.parent : bone;
        
        GameObject hitbox = Instantiate(hitboxPrefab, boneTransforms[hitboxBone][frameIndex], Quaternion.identity);
        hitbox.transform.SetParent(hitboxBone, true);
        float scaleMultiplier = 1.0f;
        hitbox.transform.localScale *= scaleMultiplier;

        // Stable ID: ParentBone_Clip_Frame
        string id = $"{hitboxBone.name}_{clip.name}_{frameIndex}";
        var hbComp = hitbox.GetComponent<hitbox>();
        if (hbComp != null)
        {
            hbComp.HitboxID = id;
        }
        else
        {
            Debug.LogWarning("Generated hitbox prefab has no 'hitbox' component attached.");
        }

        hitbox.name = $"hitbox_{id}";

        Debug.Log($"Generated hitbox {hitbox.name} at frame {frameIndex} for bone {hitboxBone.name}");
        return hitbox;
    }

    public void ClearHitboxes()
    {
        // Ensure the list is cleared before searching again.
        allHitboxesInHierarchy.Clear();

        // Check if the target model is assigned
        if (targetModel == null)
        {
            Debug.LogError("Target model not assigned for clearing hitboxes.");
            return;
        }

        // Collect all hitboxes in the character's hierarchy, including inactive ones
        foreach (Transform child in targetModel.GetComponentsInChildren<Transform>(true)) // 'true' includes inactive objects
        {
            if (child.name.ToLower().Contains("hitbox")) // Adjust if your hitboxes have a specific naming convention or tag
            {
                allHitboxesInHierarchy.Add(child.gameObject);
            }
        }

        // Delete all collected hitboxes from the scene
        foreach (GameObject hitbox in allHitboxesInHierarchy)
        {
            DestroyImmediate(hitbox); // Immediate deletion, works in edit mode as well
        }

        // Clear the list to reflect that there are no hitboxes in the hierarchy now
        allHitboxesInHierarchy.Clear();
        
        Debug.Log("Cleared all hitboxes from the character's hierarchy.");
    }


    // Store references to all hitbox GameObjects in the character's hierarchy
    public List<GameObject> allHitboxesInHierarchy = new List<GameObject>();

    public void FindAndStoreHitboxes()
    {
        allHitboxesInHierarchy.Clear(); // Clear any previous references

        // Traverse the character hierarchy to find all hitboxes, including nested children
        foreach (Transform child in targetModel.GetComponentsInChildren<Transform>(true)) // 'true' includes inactive objects
        {
            // Check for hitbox in the name or tag
            if (child.gameObject.name.ToLower().Contains("hitbox"))
            {
                allHitboxesInHierarchy.Add(child.gameObject);
            }
        }

        Debug.Log($"Stored references to {allHitboxesInHierarchy.Count} hitbox(es) in the character hierarchy.");
    }


    public void ClearAllHitboxesInHierarchy()
    {
        // Ensure we have a list of hitboxes stored
        if (allHitboxesInHierarchy.Count == 0)
        {
            FindAndStoreHitboxes();
        }

        // Destroy each hitbox GameObject and clear the list
        foreach (var hitbox in allHitboxesInHierarchy)
        {
            if (hitbox != null)
            {
                DestroyImmediate(hitbox); // Use DestroyImmediate to remove immediately in edit mode
            }
        }

        allHitboxesInHierarchy.Clear(); // Clear the references list
        hitboxFrameRanges.Clear(); // Clear any existing frame ranges
        activeHitboxes.Clear(); // Clear any active hitbox references

        Debug.Log("All hitboxes in the hierarchy have been cleared.");
    }

    // Context Menu Option to Find and Store Hitboxes
    [ContextMenu("Find and Store Hitboxes")]
    private void ContextMenuFindAndStoreHitboxes()
    {
        FindAndStoreHitboxes();
    }

    // Context Menu Option to Clear All Hitboxes in Hierarchy
    [ContextMenu("Clear All Hitboxes in Hierarchy")]
    private void ContextMenuClearAllHitboxesInHierarchy()
    {
        ClearAllHitboxesInHierarchy();
    }

    // Context Menu Option to Clear Hitboxes
    [ContextMenu("Clear Hitboxes")]
    private void ContextMenuClearHitboxes()
    {
        ClearHitboxes();
    }

#if UNITY_EDITOR
    private void SetAnimationEvent(float time, string functionName, string hitboxId)
    {
        if (clip == null)
        {
            Debug.LogError("Animation clip is null. Cannot add animation event.");
            return;
        }

        // Get the existing events on the animation clip
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);

        // Avoid duplicates matching time, function, and ID
        foreach (AnimationEvent evt in events)
        {
            if (Mathf.Approximately(evt.time, time) && evt.functionName == functionName && evt.stringParameter == hitboxId)
            {
                Debug.Log($"Animation event '{functionName}' for '{hitboxId}' at time {time} already exists, skipping.");
                return;
            }
        }

        // Create the new animation event
        AnimationEvent newEvent = new AnimationEvent
        {
            time = time,
            functionName = functionName,
            stringParameter = hitboxId
        };

        // Add the new event to the list
        List<AnimationEvent> eventsList = new List<AnimationEvent>(events)
        {
            newEvent
        };

        // Set the updated events back to the animation clip
        AnimationUtility.SetAnimationEvents(clip, eventsList.ToArray());

        Debug.Log($"Added animation event '{functionName}' at time {time} for '{hitboxId}'.");
    }
#endif

    public void GenerateHitboxesForAnimation()
    {
        if (hitboxPrefab == null)
        {
            Debug.LogError("Hitbox prefab is not assigned.");
            return;
        }

        InitializeBoneTransforms();

        if (boneTransforms == null || boneTransforms.Count == 0)
        {
            Debug.LogError("BoneTransforms dictionary is not initialized or empty.");
            return;
        }

        Debug.Log($"Starting hitbox generation for animation: {clip.name}");
        float totalTime = clip.length;
        int totalFrames = Mathf.CeilToInt(totalTime * clip.frameRate);

        // Track which bones have hitboxes
        HashSet<Transform> bonesWithHitboxes = new HashSet<Transform>();

        Transform[] animatorChildren = animator.GetComponentsInChildren<Transform>();

        // Loop through bones to create and add animation events for hitboxes
        foreach (Transform bone in animatorChildren)
        {
            Transform rootBone = animatorChildren[0];
            // Skip bones based on name
            if (bone.name.ToLower().Contains("unused") || bone.name.ToLower().Contains("adj") || bone.name.ToLower().Contains("middle"))
            {
                continue;
            }

            bool isHitboxActive = false;
            int startFrame = 0;
            string currentHitboxId = null;

            for (int frame = 0; frame < totalFrames; frame++)
            {
                float time = frame / clip.frameRate;
                clip.SampleAnimation(targetModel, time);
                AnalyzeFrame(frame, time, bone);

                if (frame > 0)
                {
                    Vector3 movement = bone.position - boneTransforms[bone][frame - 1];
                    float movementMagnitude = movement.magnitude;

                    // Detect the start of significant movement
                    if (!isHitboxActive && movementMagnitude > MovementThreshold)
                    {
                        // Create the hitbox if it doesn't already exist
                        if (!bonesWithHitboxes.Contains(bone))
                        {
                            startFrame = frame;
                            // Create the hitbox and record its ID for events
                            GameObject created = CreateHitbox(bone, frame);
                            var createdHb = created != null ? created.GetComponent<hitbox>() : null;
                            bonesWithHitboxes.Add(bone);
                            isHitboxActive = true;

                            if (createdHb != null)
                            {
                                SetAnimationEvent(time, "ActivateHitbox", createdHb.HitboxID);
                                // Cache current ID for deactivation pairing
                                currentHitboxId = createdHb.HitboxID;
                            }

                            Debug.Log($"Hitbox creation triggered for {bone.name} at frame {frame}.");
                        }
                    }

                    // Detect when to deactivate the hitbox (end of movement)
                    else if (isHitboxActive && movementMagnitude < DeactivationThreshold)
                    {
                        if (bonesWithHitboxes.Contains(bone))
                        {
                            if (!string.IsNullOrEmpty(currentHitboxId))
                            {
                                SetAnimationEvent(time, "DeactivateHitbox", currentHitboxId);
                            }
                            bonesWithHitboxes.Remove(bone);
                            isHitboxActive = false;
                            currentHitboxId = null;

                            Debug.Log($"Hitbox deactivation triggered for {bone.name} at frame {frame}.");
                        }
                    }
                }
            }
        }

        Debug.Log($"Completed hitbox generation for animation: {clip.name}");
    }

    // Add an animation event to the animation clip
    void AddAnimationEvent(AnimationClip clip, float time, string functionName, string boneName)
    {
        UnityEngine.AnimationEvent animEvent = new UnityEngine.AnimationEvent
        {
            time = time,  // The time in the animation clip to trigger the event
            functionName = functionName,  // The function to call (either "ActivateHitbox" or "DeactivateHitbox")
            stringParameter = boneName  // The bone name to pass as a parameter
        };

        clip.AddEvent(animEvent);
        Debug.Log($"{clip.name} Animation event added: {functionName} at {time} for bone {boneName}");
    }

    // Create a hitbox for a given bone at a specific frame
    GameObject CreateHitbox(Transform bone, int frameIndex)
    {
        // Check if the bone has a parent and if so, assign the hitbox to the parent bone
        Transform hitboxBone = bone.parent != null && bone.parent != this.transform ? bone.parent : bone;
        
        GameObject hitbox = Instantiate(hitboxPrefab, boneTransforms[hitboxBone][frameIndex], Quaternion.identity);
        hitbox.transform.SetParent(hitboxBone, true);
        float scaleMultiplier = 1.0f;
        hitbox.transform.localScale *= scaleMultiplier;
    
        // Stable ID: ParentBone_Clip_Frame
        string id = $"{hitboxBone.name}_{clip.name}_{frameIndex}";
        var hbComp = hitbox.GetComponent<hitbox>();
        if (hbComp != null)
        {
            hbComp.HitboxID = id;
        }
        else
        {
            Debug.LogWarning("Generated hitbox prefab has no 'hitbox' component attached.");
        }
    
        hitbox.name = $"hitbox_{id}";
        Debug.Log($"Generated hitbox at frame {frameIndex} for bone {hitboxBone.name} with id {id}");
        return hitbox;
    }

#if UNITY_EDITOR
    private void PlayAnimationInEditor()
    {
        if (clip == null)
        {
            Debug.LogError("AnimationClip is missing during playback.");
            return;
        }

        clip.SampleAnimation(targetModel, currentTime);
        EditorUtility.SetDirty(targetModel);
    }
#endif

    private void RepaintScene() 
    {
        #if UNITY_EDITOR
        SceneView.RepaintAll();
        #endif
    }

    private void OnDrawGizmos()
    {
        if (isPlaying && targetModel != null)
        {
            Gizmos.color = playbackIndicatorColor;
            Gizmos.DrawWireSphere(targetModel.transform.position, 0.1f);
        }
    }

    public void StartPlayback()
    {
        if (clip == null || targetModel == null)
        {
            Debug.LogError("Cannot start playback. Clip or Target Model is missing.");
            return;
        }

        InitializeBoneTransforms();
        currentTime = 0f;
        isPlaying = true;
#if UNITY_EDITOR
        lastEditorTime = EditorApplication.timeSinceStartup;
#endif
    }

    public void StopPlayback()
    {
        isPlaying = false;
    }

    // Context Menu Fallbacks
    [ContextMenu("Start Playback")]
    private void ContextMenuStartPlayback()
    {
        StartPlayback();
    }

    [ContextMenu("Stop Playback")]
    private void ContextMenuStopPlayback()
    {
        StopPlayback();
    }

    [ContextMenu("Analyze Animation")]
    private void ContextMenuAnalyzeAnimation()
    {
        AnalyzeAnimation();
    }

    [ContextMenu("Generate Hitboxes")]
    private void ContextMenuGenerateHitboxes()
    {
        GenerateHitboxes();
    }
}
