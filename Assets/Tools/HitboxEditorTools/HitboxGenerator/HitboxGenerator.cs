// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;

// public class HitboxGenerator : MonoBehaviour
// {
    // public Animator animator; // Animator component of the character
    // public AnimationClip clip; // Animation clip to play
    // public float positionThreshold = 0.1f; // Threshold for significant position movement
    // public int frameRate = 30; // Frame rate for tracking movements

    // private Dictionary<string, Vector3> lastBonePositions = new Dictionary<string, Vector3>();
    // private Dictionary<string, Vector3> initialBonePositions = new Dictionary<string, Vector3>();
    // private Dictionary<string, Vector3> finalBonePositions = new Dictionary<string, Vector3>();
    // private bool isTracking = false;

    // public void GenerateHitboxes()
    // {
    //     if (animator == null || clip == null)
    //     {
    //         Debug.LogError("Animator or animation clip is not assigned.");
    //         return;
    //     }

    //     // Initialize the bone positions
    //     InitializeBonePositions();

    //     // Start tracking movements frame by frame
    //     StartCoroutine(TrackBoneMovements());
    // }

    // void InitializeBonePositions()
    // {
    //     lastBonePositions.Clear();
    //     initialBonePositions.Clear();
    //     foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
    //     {
    //         Vector3 bonePosition = bone.position;
    //         lastBonePositions[bone.name] = bonePosition;
    //         initialBonePositions[bone.name] = bonePosition;
    //         Debug.Log($"Initialized position for bone {bone.name} at {bonePosition}");
    //     }
    // }

    // IEnumerator TrackBoneMovements()
    // {
    //     isTracking = true;
    //     float clipLength = clip.length;
    //     float frameTime = 1f / frameRate;

    //     for (float time = 0f; time <= clipLength; time += frameTime)
    //     {
    //         // Update the animation state
    //         animator.Play(clip.name, 0, time);
    //         animator.Update(0f); // Ensure the animator updates immediately

    //         Debug.Log($"Updated animation to time: {time}");

    //         // Track the movement at this frame
    //         TrackMovementAtCurrentTime();

    //         // Simulate frame rate
    //         yield return new WaitForSecondsRealtime(frameTime);
    //     }

    //     // After animation is done, record the final bone positions
    //     RecordFinalBonePositions();

    //     // Compare initial and final positions
    //     CompareBonePositions();

    //     isTracking = false;
    // }

    // void TrackMovementAtCurrentTime()
    // {
    //     foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
    //     {
    //         if (lastBonePositions.ContainsKey(bone.name))
    //         {
    //             Vector3 lastPosition = lastBonePositions[bone.name];
    //             Vector3 currentPosition = bone.position;

    //             // Calculate the movement
    //             float movement = Vector3.Distance(lastPosition, currentPosition);

    //             if (movement > positionThreshold)
    //             {
    //                 Debug.Log($"Bone {bone.name} moved by {movement} units.");
    //             }

    //             // Update the last position
    //             lastBonePositions[bone.name] = currentPosition;
    //         }
    //     }
    // }

    // void RecordFinalBonePositions()
    // {
    //     finalBonePositions.Clear();
    //     foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
    //     {
    //         finalBonePositions[bone.name] = bone.position;
    //         Debug.Log($"Final position for bone {bone.name} at {bone.position}");
    //     }
    // }

    // void CompareBonePositions()
    // {
    //     foreach (var initialPair in initialBonePositions)
    //     {
    //         string boneName = initialPair.Key;
    //         Vector3 initialPosition = initialPair.Value;

    //         if (finalBonePositions.ContainsKey(boneName))
    //         {
    //             Vector3 finalPosition = finalBonePositions[boneName];
    //             float movement = Vector3.Distance(initialPosition, finalPosition);

    //             if (movement > positionThreshold)
    //             {
    //                 Debug.Log($"Bone {boneName} final movement detected: {movement} units.");
    //             }
    //             else
    //             {
    //                 // Debug.Log($"Bone {boneName} final movement below threshold: {movement} units.");
    //             }
    //         }
    //     }
    // }

    using System.Collections.Generic;
    using UnityEngine;

    public class HitboxGenerator : MonoBehaviour
    {
        private Animator animator;
        private float currentTime = 0f;
        public bool isPlaying = false;
        private Dictionary<Transform, Vector3[]> boneTransforms;
        private Dictionary<Transform, float[]> boneMovements;
        private Dictionary<Transform, float> boneMovementTotals;
        private double lastEditorTime = 0.0;

        [SerializeField]
        public float movementThreshold = 2.0f; // Minimum movement to generate a hitbox
        [SerializeField]
        public float deactivationThreshold = 1.2f; // Minimum movement to deactivate hitbox
        [SerializeField]
        private float hitboxCooldownTime = 0.2f; // Time to wait before generating another hitbox
        [SerializeField]
        private int historyLength = 5; // Number of frames to average for movement detection

        private Dictionary<string, Vector3> previousPositions = new Dictionary<string, Vector3>();
        private Dictionary<string, Vector3> previousVelocities = new Dictionary<string, Vector3>();
        private Dictionary<string, float> hitboxCooldowns = new Dictionary<string, float>();
        private Queue<float> movementHistory = new Queue<float>();

        public void GenerateHitboxes()
        {
            Transform[] bones = animator.GetComponentsInChildren<Transform>();
            
            foreach (var bone in bones)
            {
                string boneName = bone.name;

                // Get current position and velocity
                Vector3 currentPosition = bone.position;
                Vector3 currentVelocity = GetVelocity(boneName, currentPosition);
                float movementMagnitude = currentVelocity.magnitude;

                // Calculate acceleration based on velocity changes
                Vector3 currentAcceleration = GetAcceleration(boneName, currentVelocity);
                float accelerationMagnitude = currentAcceleration.magnitude;

                // Add to movement history for averaging
                AddToMovementHistory(movementMagnitude);

                // Check if we should generate a hitbox
                if (ShouldGenerateHitbox(boneName, movementMagnitude, accelerationMagnitude))
                {
                    // Generate hitbox logic (replace with your actual hitbox generation code)
                    GenerateHitbox(boneName, currentPosition);
                    hitboxCooldowns[boneName] = Time.time; // Reset cooldown
                }

                // Store previous position and velocity for next frame
                previousPositions[boneName] = currentPosition;
                previousVelocities[boneName] = currentVelocity;
            }
        }

        private Vector3 GetVelocity(string boneName, Vector3 currentPosition)
        {
            if (!previousPositions.ContainsKey(boneName))
            {
                previousPositions[boneName] = currentPosition; // Initialize if not set
                return Vector3.zero; // No movement yet
            }

            Vector3 previousPosition = previousPositions[boneName];
            return (currentPosition - previousPosition) / Time.deltaTime; // Calculate velocity
        }

        private Vector3 GetAcceleration(string boneName, Vector3 currentVelocity)
        {
            if (!previousVelocities.ContainsKey(boneName))
            {
                previousVelocities[boneName] = currentVelocity; // Initialize if not set
                return Vector3.zero; // No acceleration yet
            }

            Vector3 previousVelocity = previousVelocities[boneName];
            return (currentVelocity - previousVelocity) / Time.deltaTime; // Calculate acceleration
        }

        private void AddToMovementHistory(float movementMagnitude)
        {
            if (movementHistory.Count >= historyLength)
                movementHistory.Dequeue();

            movementHistory.Enqueue(movementMagnitude);
        }

        private bool ShouldGenerateHitbox(string boneName, float movementMagnitude, float accelerationMagnitude)
        {
            // Check cooldown
            if (hitboxCooldowns.ContainsKey(boneName) && Time.time - hitboxCooldowns[boneName] < hitboxCooldownTime)
                return false;

            // Check against movement and acceleration thresholds
            return movementMagnitude > movementThreshold || accelerationMagnitude > movementThreshold;
        }

        private void GenerateHitbox(string boneName, Vector3 position)
        {
            // Your hitbox generation logic goes here
            Debug.Log($"Generated hitbox for {boneName} at position {position}");
        }

        private void OnDrawGizmos()
        {
            // Optional: Draw hitboxes or any relevant visualizations in the scene view
        }

        [ContextMenu("Generate Hitboxes")]
        private void ContextMenuGenerateHitboxes()
        {
            GenerateHitboxes();
        }
    }

// }
