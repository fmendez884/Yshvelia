using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

public class UpdateAnimatorReferences : Editor
{
    [MenuItem("Tools/Update Animator References")]
    public static void UpdateAnimatorController()
    {
        // Get the selected Animator Controller in the Project window
        AnimatorController animatorController = Selection.activeObject as AnimatorController;
        
        if (animatorController == null)
        {
            Debug.LogError("Select an Animator Controller in the Project window.");
            return;
        }

        // Extract the name of the GameObject from the Animator Controller name
        string gameObjectName = animatorController.name.ToLower();  // Use this for folder structure
        string baseFolder = "Assets/Animations/";
        string newClipsFolderPath = Path.Combine(baseFolder, gameObjectName + "_animations");

        // Ensure the folder exists
        if (!Directory.Exists(newClipsFolderPath))
        {
            Debug.LogError($"New clips folder not found: {newClipsFolderPath}");
            return;
        }

        // Iterate through all states in the Animator Controller
        foreach (var state in animatorController.layers[0].stateMachine.states)
        {
            if (state.state.motion is AnimationClip oldClip && oldClip != null)
            {
                string oldClipName = oldClip.name;

                // Replace '|' with '_' to match new clip naming convention
                string newClipName = oldClipName.Replace("|", "_");

                // Create the path to the new clip
                string newClipPath = Path.Combine(newClipsFolderPath, newClipName + ".anim");

                // Ensure the new clip exists
                if (File.Exists(newClipPath))
                {
                    // Load the new clip
                    AnimationClip newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newClipPath);
                    if (newClip != null)
                    {
                        // Replace the old clip with the new one in the Animator state
                        state.state.motion = newClip;
                        Debug.Log($"Updated state '{state.state.name}' to use new clip: {newClip.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to load new clip at path: {newClipPath}");
                    }
                }
                else
                {
                    Debug.LogWarning($"New clip not found for state '{state.state.name}' (expected at: {newClipPath}).");
                }
            }
        }

        Debug.Log("Animator Controller updated!");
    }
}
