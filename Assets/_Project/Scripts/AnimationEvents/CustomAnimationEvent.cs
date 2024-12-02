using System;
using UnityEngine.Events;

[Serializable]
public class CustomAnimationEvent {
    public string eventName;
    public UnityEvent OnAnimationEvent;
}