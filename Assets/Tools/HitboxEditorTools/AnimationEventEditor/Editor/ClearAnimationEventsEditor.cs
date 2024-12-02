using UnityEngine;
using UnityEditor;

public class ClearAnimationEventsEditor : EditorWindow
{
    private AnimationClip animationClip;
    private GameObject targetObject;

    [MenuItem("Tools/Clear Animation Events")]
    public static void ShowWindow()
    {
        GetWindow<ClearAnimationEventsEditor>("Clear Animation Events");
    }

    private void OnGUI()
    {
        GUILayout.Label("Clear Animation Events", EditorStyles.boldLabel);

        // Single clip option
        animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", animationClip, typeof(AnimationClip), false);
        if (GUILayout.Button("Clear Events (Single Clip)"))
        {
            if (animationClip == null)
            {
                Debug.LogError("No Animation Clip assigned.");
                return;
            }

            ClearAnimationEvents(animationClip);
        }

        GUILayout.Space(10);

        // Batch option
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object (FBX or GameObject)", targetObject, typeof(GameObject), true);
        if (GUILayout.Button("Clear Events (All Clips)"))
        {
            if (targetObject == null)
            {
                Debug.LogError("No Target Object assigned.");
                return;
            }

            ClearEventsForAllClips(targetObject);
        }
    }

    private void ClearAnimationEvents(AnimationClip clip)
    {
        // Get all events from the clip
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);

        if (events.Length == 0)
        {
            Debug.Log($"No Animation Events found in the clip: {clip.name}");
            return;
        }

        // Clear the events
        AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
        Debug.Log($"Cleared {events.Length} events from the clip: {clip.name}");
    }

    private void ClearEventsForAllClips(GameObject obj)
    {
        // Get all Animation Clips associated with the object
        AnimationClip[] clips = AnimationUtility.GetAnimationClips(obj);

        if (clips.Length == 0)
        {
            Debug.LogError("No Animation Clips found on the GameObject: " + obj.name);
            return;
        }

        int clearedClips = 0;
        foreach (var clip in clips)
        {
            ClearAnimationEvents(clip);
            clearedClips++;
        }

        Debug.Log($"Cleared events from {clearedClips} Animation Clips in the GameObject: {obj.name}");
    }
}
