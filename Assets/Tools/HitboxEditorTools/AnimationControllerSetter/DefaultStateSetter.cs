using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class DefaultStateTransitionAdder : EditorWindow
{
    private AnimatorController animatorController;

    [MenuItem("Tools/Add Conditional Transitions from Default State")]
    public static void ShowWindow()
    {
        GetWindow<DefaultStateTransitionAdder>("Default State Transitions");
    }

    private void OnGUI()
    {
        GUILayout.Label("Select Animator Controller", EditorStyles.boldLabel);

        animatorController = (AnimatorController)EditorGUILayout.ObjectField("Animator Controller", animatorController, typeof(AnimatorController), true);

        if (animatorController != null)
        {
            if (GUILayout.Button("Add Conditional Transitions To/From Default State"))
            {
                AddConditionalTransitionsToFromDefaultState();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Please select an Animator Controller.", MessageType.Warning);
        }
    }

    private void AddConditionalTransitionsToFromDefaultState()
    {
        if (animatorController == null)
        {
            Debug.LogError("AnimatorController not assigned.");
            return;
        }

        // Get the first layer's state machine
        AnimatorControllerLayer layer = animatorController.layers[0];
        AnimatorStateMachine stateMachine = layer.stateMachine;

        // Find the default state (entry state) in the first layer
        AnimatorState defaultState = layer.stateMachine.defaultState;
        if (defaultState == null)
        {
            Debug.LogError("No default state found in Animator Controller.");
            return;
        }

        // Check if the trigger parameter exists
        string triggerParam = "StartAnimation";
        bool parameterExists = false;
        foreach (var parameter in animatorController.parameters)
        {
            if (parameter.name == triggerParam)
            {
                parameterExists = true;
                break;
            }
        }

        // Add the parameter if it doesn't exist
        if (!parameterExists)
        {
            animatorController.AddParameter(triggerParam, AnimatorControllerParameterType.Trigger);
        }

        // Loop through all states to add conditional transitions
        foreach (var state in stateMachine.states)
        {
            if (state.state != defaultState) // Skip the default state itself
            {
                // Conditional Transition from Default to Current State
                bool transitionToExists = false;
                foreach (var transition in defaultState.transitions)
                {
                    if (transition.destinationState == state.state)
                    {
                        transitionToExists = true;
                        break;
                    }
                }

                if (!transitionToExists)
                {
                    var newTransitionTo = defaultState.AddTransition(state.state);
                    newTransitionTo.hasExitTime = false; // No exit time for default state
                    newTransitionTo.AddCondition(AnimatorConditionMode.If, 0, triggerParam);
                    Debug.Log($"Added conditional transition from '{defaultState.name}' to '{state.state.name}'.");
                }
                else
                {
                    // If transition already exists, modify its exit time
                    foreach (var transition in defaultState.transitions)
                    {
                        if (transition.destinationState == state.state)
                        {
                            transition.hasExitTime = false;
                            Debug.Log($"Modified transition from '{defaultState.name}' to '{state.state.name}', setting hasExitTime to false.");
                        }
                    }
                }

                // Transition from Current State back to Default
                bool transitionFromExists = false;
                foreach (var transition in state.state.transitions)
                {
                    if (transition.destinationState == defaultState)
                    {
                        transitionFromExists = true;
                        break;
                    }
                }

                if (!transitionFromExists)
                {
                    var newTransitionFrom = state.state.AddTransition(defaultState);
                    newTransitionFrom.hasExitTime = true; // Use exit time to complete the animation
                    newTransitionFrom.exitTime = 0.95f; // Adjust exit time as needed
                    Debug.Log($"Added transition from '{state.state.name}' back to '{defaultState.name}'.");
                }
                else
                {
                    // If transition already exists, modify its exit time
                    foreach (var transition in state.state.transitions)
                    {
                        if (transition.destinationState == defaultState)
                        {
                            transition.hasExitTime = false;
                            Debug.Log($"Modified transition from '{state.state.name}' to '{defaultState.name}', setting hasExitTime to false.");
                        }
                    }
                }
            }
        }
    }
}
