using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
[CustomEditor(typeof(HitboxTool))]
public class HitboxToolEditor : Editor
{
    // Store the foldout states for each parent
    private Dictionary<Transform, bool> foldouts = new Dictionary<Transform, bool>();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HitboxTool HitboxTool = (HitboxTool)target;

        GUILayout.Space(10);

        // Playback Controls
        if (HitboxTool.isPlaying)
        {
            if (GUILayout.Button("Stop Playback"))
            {
                HitboxTool.StopPlayback();
            }
        }
        else
        {
            if (GUILayout.Button("Start Playback"))
            {
                HitboxTool.StartPlayback();
            }
        }

        if (GUILayout.Button("Analyze Animation"))
        {
            HitboxTool.AnalyzeAnimation();
        }

        if (HitboxTool.isPlaying)
        {
            EditorGUILayout.HelpBox("Animation is playing...", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Animation is stopped.", MessageType.Warning);
        }

        if (GUILayout.Button("Generate Hitboxes"))
        {
            HitboxTool.GenerateHitboxes();
        }

        if (GUILayout.Button("Generate Hitboxes for Animation"))
        {
            HitboxTool.GenerateHitboxesForAnimation();
        }

        // Add buttons for finding and clearing hitboxes
        if (GUILayout.Button("Find and Store Hitboxes"))
        {
            HitboxTool.FindAndStoreHitboxes();
        }

        if (GUILayout.Button("Clear All Hitboxes in Hierarchy"))
        {
            HitboxTool.ClearAllHitboxesInHierarchy();
        }

        // Display stored hitboxes in hierarchy
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stored Hitboxes in Hierarchy", EditorStyles.boldLabel);

        if (HitboxTool.allHitboxesInHierarchy.Count == 0)
        {
            EditorGUILayout.HelpBox("No hitboxes stored. Click 'Find and Store Hitboxes' to populate the list.", MessageType.Info);
        }
        else
        {
            // Organize hitboxes by parent
            Dictionary<Transform, List<GameObject>> hitboxesByParent = new Dictionary<Transform, List<GameObject>>();

            foreach (GameObject hitbox in HitboxTool.allHitboxesInHierarchy)
            {
                if (hitbox != null)
                {
                    Transform parent = hitbox.transform.parent;
                    if (parent != null)
                    {
                        if (!hitboxesByParent.ContainsKey(parent))
                        {
                            hitboxesByParent[parent] = new List<GameObject>();
                        }
                        hitboxesByParent[parent].Add(hitbox);
                    }
                }
            }

            // Display the hitboxes in a collapsible format
            foreach (var kvp in hitboxesByParent)
            {
                Transform parent = kvp.Key;
                List<GameObject> hitboxes = kvp.Value;

                // Create a foldout for each parent
                foldouts[parent] = EditorGUILayout.Foldout(foldouts.GetValueOrDefault(parent, false), parent.name, true);
                if (foldouts[parent])
                {
                    foreach (GameObject hitbox in hitboxes)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"    {hitbox.name}"); // Indent hitbox names

                        if (GUILayout.Button("Select"))
                        {
                            Selection.activeObject = hitbox; // Selects the hitbox in the hierarchy
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        // Mark the HitboxTool as dirty to save changes in editor
        EditorUtility.SetDirty(HitboxTool); 
    }
}
#endif
