using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class ClearAnimationEvents : MonoBehaviour
{
    [Tooltip("The Animation Clip to clear events from")]
    public AnimationClip animationClip;

    void Start()
    {
        if (animationClip == null)
        {
            Debug.LogError("No Animation Clip assigned.");
            return;
        }

        // Get all events from the clip
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(animationClip);

        if (events.Length == 0)
        {
            Debug.Log("No Animation Events found in the clip: " + animationClip.name);
            return;
        }

        // Log the number of events before clearing
        Debug.Log($"Clearing {events.Length} events from the clip: {animationClip.name}");

        // Clear the events
        AnimationUtility.SetAnimationEvents(animationClip, new AnimationEvent[0]);

        // Verify that the events are cleared
        if (AnimationUtility.GetAnimationEvents(animationClip).Length == 0)
        {
            Debug.Log("Successfully cleared all events.");
        }
        else
        {
            Debug.LogError("Failed to clear the events.");
        }
    }
}
