using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public class AnimationControllerSetter : EditorWindow
{
    private GameObject character;
    private Animator animator;
    private AnimatorController animatorController;
    private Object fbxAsset;

    [MenuItem("Tools/Add Animation Clips to Controller")]
    public static void ShowWindow()
    {
        GetWindow<AnimationControllerSetter>("Animation Controller Setter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Select Character, Animator Controller, and FBX Asset", EditorStyles.boldLabel);

        character = (GameObject)EditorGUILayout.ObjectField("Character", character, typeof(GameObject), true);
        animatorController = (AnimatorController)EditorGUILayout.ObjectField("Animator Controller", animatorController, typeof(AnimatorController), true);
        fbxAsset = EditorGUILayout.ObjectField("FBX Asset", fbxAsset, typeof(Object), false);

        if (character != null && fbxAsset != null)
        {
            animator = character.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                EditorGUILayout.HelpBox("Selected character must have an Animator with an AnimatorController assigned.", MessageType.Error);
                return;
            }

            if (GUILayout.Button("Add Animation Clips as States"))
            {
                Debug.Log("Button pressed: Adding Animation Clips as States");
                AddAnimationClipsToController();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Please select both a character and an FBX asset.", MessageType.Warning);
        }
    }

    private void AddAnimationClipsToController()
    {
        if (animatorController == null)
        {
            Debug.LogError("AnimatorController not found on the selected character.");
            return;
        }

        Debug.Log("Animator Controller found, proceeding to add animation clips.");

        // Ensure the FBX asset path is valid
        var fbxPath = AssetDatabase.GetAssetPath(fbxAsset);
        if (string.IsNullOrEmpty(fbxPath))
        {
            Debug.LogError("FBX asset path not found.");
            return;
        }

        AnimationClipExtractor.fbxAsset = fbxAsset;

        // Call AnimationClipExtractor to duplicate the clips
        // AnimationClipExtractor.DuplicateClips();

        AnimationClipExtractor.EnsureAnimationsImportedAndDuplicate();

        // Now, find the newly duplicated clips
        string targetFolder = $"{character.name}_animations";
        string targetDirectory = $"Assets/Animations/{targetFolder}";
        var newClips = AssetDatabase.FindAssets("t:AnimationClip", new[] { targetDirectory });

        List<AnimationClip> duplicatedClips = new List<AnimationClip>();
        foreach (var guid in newClips)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            duplicatedClips.Add(clip);
            Debug.Log($"Found duplicated clip: {clip.name}");
        }

        // Keep track of existing states
        HashSet<string> existingStates = new HashSet<string>();
        foreach (var state in animatorController.layers[0].stateMachine.states)
        {
            existingStates.Add(state.state.name);
        }

        // Add each animation clip as a state if not already in the controller
        foreach (var newClip in duplicatedClips)
        {
            string uniqueName = newClip.name;
            int counter = 1;

            // Make sure the clip name is unique
            while (existingStates.Contains(uniqueName))
            {
                uniqueName = $"{newClip.name}_{counter++}";
            }

            // Update the clip name to be unique
            newClip.name = uniqueName;

            // Add the clip as a state in the Animator Controller
            var newState = animatorController.AddMotion(newClip);
            newState.name = uniqueName;
            Debug.Log($"Added animation state: {newState.name}");

            // Add the name to existing states to avoid future conflicts
            existingStates.Add(newState.name);
        }
    }

}
