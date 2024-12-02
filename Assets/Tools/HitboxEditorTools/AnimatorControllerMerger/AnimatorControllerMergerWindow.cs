using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class AnimatorControllerMergerWindow : EditorWindow
{
    private AnimatorController sourceController1;
    private AnimatorController sourceController2;
    private string newControllerName = "MergedAnimatorController";
    private string savePath = "Assets";

    [MenuItem("Tools/Animator Controller Merger")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorControllerMergerWindow>("Animator Controller Merger");
    }

    private void OnGUI()
    {
        GUILayout.Label("Merge Animator Controllers", EditorStyles.boldLabel);

        // Input fields for the Animator Controllers
        sourceController1 = (AnimatorController)EditorGUILayout.ObjectField("Source Controller 1", sourceController1, typeof(AnimatorController), false);
        sourceController2 = (AnimatorController)EditorGUILayout.ObjectField("Source Controller 2", sourceController2, typeof(AnimatorController), false);

        newControllerName = EditorGUILayout.TextField("New Controller Name", newControllerName);
        savePath = EditorGUILayout.TextField("Save Path", savePath);

        // Add a button to perform the merge
        if (GUILayout.Button("Merge"))
        {
            if (sourceController1 != null && sourceController2 != null)
            {
                MergeAnimatorControllers();
            }
            else
            {
                Debug.LogError("Please assign both source AnimatorControllers.");
            }
        }
    }

    private void MergeAnimatorControllers()
    {
        // Create a new Animator Controller
        AnimatorController newController = AnimatorController.CreateAnimatorControllerAtPath($"{savePath}/{newControllerName}.controller");

        // Merge layers and states from the first source controller
        MergeLayers(sourceController1, newController);

        // Merge layers and states from the second source controller
        MergeLayers(sourceController2, newController);

        Debug.Log($"Merged Animator Controller created at {savePath}/{newControllerName}.controller");
    }

    private void MergeLayers(AnimatorController sourceController, AnimatorController targetController)
    {
        foreach (var sourceLayer in sourceController.layers)
        {
            // Duplicate the layer
            AnimatorControllerLayer newLayer = new AnimatorControllerLayer
            {
                name = sourceLayer.name,
                defaultWeight = sourceLayer.defaultWeight,
                blendingMode = sourceLayer.blendingMode,
                avatarMask = sourceLayer.avatarMask,
                stateMachine = new AnimatorStateMachine()
            };

            // Copy states from the source layer
            CopyStateMachine(sourceLayer.stateMachine, newLayer.stateMachine);

            // Add the layer to the target controller
            var layers = targetController.layers;
            System.Array.Resize(ref layers, layers.Length + 1);
            layers[layers.Length - 1] = newLayer;
            targetController.layers = layers;
        }
    }

    private void CopyStateMachine(AnimatorStateMachine sourceStateMachine, AnimatorStateMachine targetStateMachine)
    {
        // Copy states
        foreach (var sourceState in sourceStateMachine.states)
        {
            AnimatorState newState = targetStateMachine.AddState(sourceState.state.name);
            newState.motion = sourceState.state.motion;
            newState.behaviours = sourceState.state.behaviours;
            newState.speed = sourceState.state.speed;
            newState.cycleOffset = sourceState.state.cycleOffset;
            newState.mirror = sourceState.state.mirror;
        }

        // Copy transitions
        foreach (var sourceTransition in sourceStateMachine.anyStateTransitions)
        {
            var newTransition = targetStateMachine.AddAnyStateTransition(sourceTransition.destinationState);
            CopyTransitionProperties(sourceTransition, newTransition);
        }

        // Recursively copy sub-state machines
        foreach (var sourceSubStateMachine in sourceStateMachine.stateMachines)
        {
            var newSubStateMachine = targetStateMachine.AddStateMachine(sourceSubStateMachine.stateMachine.name);
            CopyStateMachine(sourceSubStateMachine.stateMachine, newSubStateMachine);
        }
    }

    private void CopyTransitionProperties(AnimatorStateTransition source, AnimatorStateTransition target)
    {
        target.conditions = source.conditions;
        target.duration = source.duration;
        target.exitTime = source.exitTime;
        target.hasExitTime = source.hasExitTime;
        target.hasFixedDuration = source.hasFixedDuration;
        target.interruptionSource = source.interruptionSource;
        target.canTransitionToSelf = source.canTransitionToSelf;
    }
}
