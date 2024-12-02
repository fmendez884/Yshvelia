using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Linq;

public class AnimatorControllerMerger : MonoBehaviour
{
    public AnimatorController sourceController1;
    public AnimatorController sourceController2;
    public string savePath = "Assets/Animations";
    public string mergedControllerName;

    public void MergeControllers()
    {
        if (sourceController1 == null || sourceController2 == null)
        {
            Debug.LogError("Both source Animator Controllers must be assigned.");
            return;
        }

        string newName = $"{sourceController1.name}_{sourceController2.name}";
        mergedControllerName = string.IsNullOrEmpty(mergedControllerName) ? newName : mergedControllerName;

        string path = $"{savePath}/{mergedControllerName}.controller";

        // Create a new Animator Controller
        AnimatorController mergedController = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Copy layers and parameters from sourceController1
        CopyLayersAndParameters(sourceController1, mergedController);

        // Copy layers and parameters from sourceController2
        CopyLayersAndParameters(sourceController2, mergedController);

        Debug.Log($"Animator Controllers merged successfully into {mergedControllerName}.");
    }

    private void CopyLayersAndParameters(AnimatorController source, AnimatorController target)
    {
        // Copy parameters
        foreach (var param in source.parameters)
        {
            if (!target.parameters.Any(p => p.name == param.name))
            {
                target.AddParameter(param.name, param.type);
                Debug.Log($"Added parameter: {param.name}");
            }
        }

        // Copy layers
        foreach (var sourceLayer in source.layers)
        {
            AnimatorControllerLayer newLayer = new AnimatorControllerLayer
            {
                name = sourceLayer.name,
                defaultWeight = sourceLayer.defaultWeight,
                blendingMode = sourceLayer.blendingMode,
                avatarMask = sourceLayer.avatarMask
            };

            // Duplicate the state machine
            newLayer.stateMachine = CopyStateMachine(sourceLayer.stateMachine);

            // Add the new layer to the target controller
            target.AddLayer(newLayer);
            Debug.Log($"Added layer: {sourceLayer.name}");
        }
    }

    private AnimatorStateMachine CopyStateMachine(AnimatorStateMachine sourceStateMachine)
    {
        AnimatorStateMachine newStateMachine = new AnimatorStateMachine();

        // Copy states
        foreach (var state in sourceStateMachine.states)
        {
            AnimatorState newState = newStateMachine.AddState(state.state.name, state.position);
            newState.motion = state.state.motion; // Copy the motion (Animation Clip)
            newState.speed = state.state.speed;
            newState.tag = state.state.tag;
            newState.writeDefaultValues = state.state.writeDefaultValues;

            Debug.Log($"Copied state: {state.state.name}");
        }

        // Copy transitions
        foreach (var transition in sourceStateMachine.anyStateTransitions)
        {
            AnimatorStateTransition newTransition = newStateMachine.AddAnyStateTransition(transition.destinationState);
            CopyTransitionSettings(transition, newTransition);
            Debug.Log($"Copied any-state transition to: {transition.destinationState.name}");
        }

        foreach (var state in sourceStateMachine.states)
        {
            foreach (var transition in state.state.transitions)
            {
                AnimatorState newState = newStateMachine.states.First(s => s.state.name == state.state.name).state;
                AnimatorStateTransition newTransition = newState.AddTransition(transition.destinationState);
                CopyTransitionSettings(transition, newTransition);
                Debug.Log($"Copied transition from {state.state.name} to {transition.destinationState.name}");
            }
        }

        // Debug: Check if any state is missing motions or transitions
        foreach (var state in newStateMachine.states)
        {
            if (state.state.motion == null)
            {
                Debug.LogWarning($"State {state.state.name} is missing a motion.");
            }
        }

        return newStateMachine;
    }

    private void CopyTransitionSettings(AnimatorStateTransition source, AnimatorStateTransition target)
    {
        target.conditions = source.conditions; // Directly assign the conditions array
        target.duration = source.duration;
        target.exitTime = source.exitTime;
        target.hasExitTime = source.hasExitTime;
        target.hasFixedDuration = source.hasFixedDuration;
        target.interruptionSource = source.interruptionSource;
        target.canTransitionToSelf = source.canTransitionToSelf;

        // Copy other transition settings if needed
    }
}
