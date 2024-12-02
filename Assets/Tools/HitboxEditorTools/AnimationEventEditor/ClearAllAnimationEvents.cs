using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class ClearAllAnimationEvents : MonoBehaviour
{
    [Tooltip("The GameObject or FBX file containing the Animation Clips")]
    public GameObject targetObject;

    void Start()
    {
        if (targetObject == null)
        {
            Debug.LogError("Target object is not assigned.");
            return;
        }

        // Get all animation clips from the GameObject
        AnimationClip[] clips = AnimationUtility.GetAnimationClips(targetObject);

        foreach (var clip in clips)
        {
            // Clear events for each clip
            AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
            Debug.Log($"Cleared events from clip: {clip.name}");
        }
    }
}
