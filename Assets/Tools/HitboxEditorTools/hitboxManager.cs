using System;
using System.Collections.Generic;
using UnityEngine;

public class HitboxManager : MonoBehaviour
{
    [Header("Hitbox Manager Settings")]
    public List<hitbox> Hitboxes = new List<hitbox>(); // Track all hitboxes
    public Animator CharacterAnimator; // Reference to the animator

    private List<Character> Characters = new List<Character>();

    private void Start()
    {
        Character character = GameObject.FindAnyObjectByType<Character>();
        Characters.Add(character);       
    }

    private void Awake()
    {
        // Automatically find and register all hitboxes in the hierarchy
        Hitboxes.AddRange(GetComponentsInChildren<hitbox>());


    }

    public void ActivateHitbox(string hitboxID)
    {
        foreach (var hb in Hitboxes)
        {
            if (hb.HitboxID == hitboxID)
            {
                hb.Activate();
                Debug.Log($"Activated hitbox: {hb.name}");
                return;
            }
        }
        Debug.LogWarning($"No hitbox found with ID: {hitboxID}");
    }

    public void DeactivateHitbox(string hitboxID)
    {
        foreach (var hb in Hitboxes)
        {
            if (hb.HitboxID == hitboxID)
            {
                hb.Deactivate();
                Debug.Log($"Deactivated hitbox: {hb.name}");
                return;
            }
        }
        Debug.LogWarning($"No hitbox found with ID: {hitboxID}");
    }

    public void DeactivateAllHitboxes()
    {
        foreach (var hb in Hitboxes)
        {
            hb.Deactivate();
        }
        Debug.Log("All hitboxes deactivated.");
    }

    private void OnHitDetected(GameObject target)
    {
        // Example: Notify combat system of a hit
        Debug.Log($"Hit detected on {target.name}");
    }
}
