using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AnimatorControllerMerger))]
public class AnimatorControllerMergerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default Inspector fields
        DrawDefaultInspector();

        // Add a button for merging Animator Controllers
        AnimatorControllerMerger merger = (AnimatorControllerMerger)target;
        if (GUILayout.Button("Merge Controllers"))
        {
            merger.MergeControllers();
        }
    }
}
