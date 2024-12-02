using UnityEngine;
using System.Collections.Generic;

public class AnimationEventReceiver : MonoBehaviour {
    [SerializeField] List<CustomAnimationEvent> animationEvents = new();

    public void OnAnimationEventTriggered(string eventName) {
        CustomAnimationEvent matchingEvent = animationEvents.Find(se => se.eventName == eventName);
        matchingEvent?.OnAnimationEvent?.Invoke();
    }
}