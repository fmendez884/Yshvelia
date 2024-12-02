using UnityEngine;

public class hitbox : MonoBehaviour
{
    [Header("Hitbox Settings")]
    public bool IsActive = false; // Determines if the hitbox is currently active
    public string HitboxID;       // Optional: A unique identifier for the hitbox (e.g., bone name)
    // public int Damage = 10;       // Damage dealt by this hitbox
    public LayerMask TargetLayer; // Specifies which layers this hitbox can interact with

    private Collider hitboxCollider;

    private void Awake()
    {
        // Ensure the hitbox has a Collider (preferably a Trigger)
        hitboxCollider = GetComponent<Collider>();
        if (hitboxCollider == null)
        {
            Debug.LogError($"Hitbox on {gameObject.name} requires a Collider!");
        }
        else if (!hitboxCollider.isTrigger)
        {
            Debug.LogWarning($"Hitbox Collider on {gameObject.name} should be set as a Trigger.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only process collisions when the hitbox is active
        if (!IsActive) return;

        // Check if the other object is on the target layer
        // if ((TargetLayer.value & (1 << other.gameObject.layer)) == 0) return;

        // // Try to apply damage if the target has a Health component
        // // Health targetHealth = other.GetComponent<Health>();
        // if (targetHealth != null)
        // {
        //     targetHealth.TakeDamage(Damage);
        //     Debug.Log($"{other.gameObject.name} hit by {gameObject.name} for {Damage} damage!");
        //     OnHit(other.gameObject); // Call a custom event or method when a hit is detected
        // }
        Debug.Log("HIT");
    }

    public void hitboxTrigger()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }

    /// <summary>
    /// Activates the hitbox, enabling it to detect collisions.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        gameObject.SetActive(true);
        Debug.Log($"{gameObject.name} hitbox activated.");
    }

    /// <summary>
    /// Deactivates the hitbox, disabling collision detection.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        gameObject.SetActive(false);
        Debug.Log($"{gameObject.name} hitbox deactivated.");
    }

    /// <summary>
    /// Custom logic when a hit is detected.
    /// </summary>
    /// <param name="hitTarget">The GameObject that was hit.</param>
    protected virtual void OnHit(GameObject hitTarget)
    {
        // Extend this method in derived classes for custom behavior
    }
}
