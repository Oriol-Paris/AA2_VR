using System.Collections.Generic;
using UnityEngine;

namespace FlickGrab
{
    /// <summary>
    /// Detects a quick "flick" or "snap" motion towards the player.
    /// </summary>
    public class FlickGestureDetector : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Minimum velocity towards the user to trigger the flick.")]
        [SerializeField] private float velocityThreshold = 1.0f;
        [Tooltip("Minimum acceleration to trigger the flick.")]
        [SerializeField] private float accelerationThreshold = 4.0f;
        [SerializeField, Range(2, 10)] private int samples = 5;

        private Queue<Vector3> velocityHistory = new Queue<Vector3>();
        private Vector3 lastPosition;
        private Vector3 averageVelocity;
        private Vector3 lastAverageVelocity;

        private void Start()
        {
            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;

            velocityHistory.Enqueue(currentVelocity);
            if (velocityHistory.Count > samples)
                velocityHistory.Dequeue();

            lastAverageVelocity = averageVelocity;
            averageVelocity = Vector3.zero;
            foreach (var v in velocityHistory)
                averageVelocity += v;
            averageVelocity /= velocityHistory.Count;
        }

        public bool IsFlicking()
        {
            // We use the world space velocity but compare it against the direction from the hand to the player
            // For simplicity, we assume pulling "back" relative to the controller's forward is what we want.
            // In a more robust system, we might use the vector to the HMD.
            
            float pullStrength = Vector3.Dot(averageVelocity, -transform.forward);
            
            Vector3 acceleration = (averageVelocity - lastAverageVelocity) / Time.deltaTime;
            float pullAcceleration = Vector3.Dot(acceleration, -transform.forward);

            return pullStrength > velocityThreshold && pullAcceleration > accelerationThreshold;
        }
    }
}
