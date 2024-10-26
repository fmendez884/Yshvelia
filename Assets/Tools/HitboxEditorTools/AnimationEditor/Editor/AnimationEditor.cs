using UnityEngine;
using UnityEditor;
using Unity.VisualScripting;
using System;
using System.Linq;
using System.Collections.Generic;

public class AnimationEditor : EditorWindow
{
    protected GameObject go;
    protected AnimationClip animationClip;
    protected float time = 0.0f;
    protected bool lockSelection = false;
    protected bool animationMode = false;
    private EditorCurveBinding[] originalBindings;
    private AnimationClip originalClip;

    [MenuItem("Examples/AnimationMode demo", false, 2000)]
    public static void DoWindow()
    {
        var window = GetWindowWithRect<AnimationEditor>(new Rect(0, 0, 300, 120));
        window.Show();
    }

    // Called when the selection changes in the editor
    public void OnSelectionChange()
    {
        if (!lockSelection)
        {
            go = Selection.activeGameObject;
            Repaint();
        }
    }

    // Main editor window GUI
    public void OnGUI()
    {
        // Wait for user to select a GameObject
        if (go == null)
        {
            EditorGUILayout.HelpBox("Please select a GameObject", MessageType.Info);
            return;
        }

        // Animate and Lock buttons. Check if Animate has changed
        GUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        bool toggleAnimation = GUILayout.Toggle(AnimationMode.InAnimationMode(), "Animate");
        if (EditorGUI.EndChangeCheck())
            ToggleAnimationMode(toggleAnimation);

        GUILayout.FlexibleSpace();
        lockSelection = GUILayout.Toggle(lockSelection, "Lock");
        GUILayout.EndHorizontal();

        // Slider to use when Animate has been ticked
        EditorGUILayout.BeginVertical();
        animationClip = EditorGUILayout.ObjectField("Animation Clip", animationClip, typeof(AnimationClip), false) as AnimationClip;
        if (animationClip != null)
        {
            float startTime = 0.0f;
            float stopTime = animationClip.length;
            time = EditorGUILayout.Slider("Time", time, startTime, stopTime);
        }
        else if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }
        EditorGUILayout.EndVertical();

        // Draw Animation Events
        DrawAnimationEvents();

        // Button to add an animation event
        if (GUILayout.Button("Add Event"))
        {
            AddAnimationEvent();
        }
    }

    void Update()
    {
        if (go == null || animationClip == null)
            return;

        // Animate the GameObject
        if (!EditorApplication.isPlaying && AnimationMode.InAnimationMode())
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(go, animationClip, time);
            AnimationMode.EndSampling();

            SceneView.RepaintAll();
        }
    }

    private void OnDestroy()
    {
        // Code to execute when the window is closed
        Debug.Log("Editor window closed!");
        ToggleAnimationMode(false);
    }

    void ToggleAnimationMode(bool enable)
    {
        if (enable)
        {
            StartAnimationMode();
        }
        else
        {
            StopAnimationMode();
        }
    }

    void StartAnimationMode()
    {
        if (go == null || animationClip == null)
            return;

        // Save the current state
        originalBindings = AnimationUtility.GetCurveBindings(animationClip);
        originalClip = new AnimationClip();
        AnimationUtility.SetAnimationClipSettings(originalClip, AnimationUtility.GetAnimationClipSettings(animationClip));
        foreach (var binding in originalBindings)
        {
            AnimationUtility.SetEditorCurve(originalClip, binding, AnimationUtility.GetEditorCurve(animationClip, binding));
        }

        // Start animation mode
        AnimationMode.StartAnimationMode();
    }

    void StopAnimationMode()
    {
        if (go == null || originalClip == null)
            return;

        // Restore the initial state
        foreach (var binding in originalBindings)
        {
            AnimationUtility.SetEditorCurve(animationClip, binding, AnimationUtility.GetEditorCurve(originalClip, binding));
        }

        AnimationMode.StopAnimationMode();
        SceneView.RepaintAll();
    }

    private void AddAnimationEvent()
    {
        if (animationClip == null)
            return;

        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(animationClip);
        

        AnimationEvent newAnimEvent = new AnimationEvent
        {
            time = time,
            functionName = "YourFunctionName"
        };

        // events.Append<AnimationEvent>(newAnimEvent);

        // List<AnimationEvent> eventsList = new List<AnimationEvent>();
        List<AnimationEvent> eventsList = new List<AnimationEvent>();

        for (int i = 0; i < events.Length; i++)
        {
            eventsList.Add(events[i]);
        }

        eventsList.Add(newAnimEvent);

        // You can convert it back to an array if you would like to
        AnimationEvent[] newEvents = eventsList.ToArray();

        // Debug.Log(events);

        AnimationUtility.SetAnimationEvents(animationClip, newEvents);
    }

    private void DrawAnimationEvents()
    {
        if (animationClip == null)
            return;

        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(animationClip);
        foreach (var animEvent in events)
        {
            float normalizedTime = animEvent.time / animationClip.length;
            Rect rect = EditorGUILayout.GetControlRect();
            rect.width *= normalizedTime;
            EditorGUI.DrawRect(rect, Color.red);
            EditorGUILayout.LabelField("Event at: " + animEvent.time.ToString("F2") + " - " + animEvent.functionName);
        }
    }
}
