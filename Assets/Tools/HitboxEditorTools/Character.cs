using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    public Animator animator;
    public hitboxContainer hitboxContainer;
    public List<hitbox> hitboxes = new List<hitbox>();
    public hitbox hitbox;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ActivateHitbox()
    {
        hitbox.gameObject.SetActive(true);
        Debug.Log($"{hitbox.name} hitbox activated.");
    }

    /// <summary>
    /// Deactivates the hitbox, disabling collision detection.
    /// </summary>
    public void DeactivateHitbox()
    {
        hitbox.gameObject.SetActive(false);
        Debug.Log($"{hitbox.name} hitbox deactivated.");
    }
}
