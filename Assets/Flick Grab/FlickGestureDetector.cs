using System.Collections.Generic;
using UnityEngine;

namespace FlickGrab
{
    public class FlickGestureDetector : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Minimum velocity towards the user to trigger the flick.")]
        [SerializeField] private float velocityThreshold = 1.5f;
        [Tooltip("Minimum acceleration to trigger the flick.")]
        [SerializeField] private float accelerationThreshold = 5f;
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
            // A flick is a sudden movement in the opposite direction of the forward vector (pulling back)
            // We use the detector's transform (the controller) for the forward vector.
            float pullStrength = Vector3.Dot(averageVelocity, -transform.forward);
            
            // Check acceleration (change in velocity)
            Vector3 acceleration = (averageVelocity - lastAverageVelocity) / Time.deltaTime;
            float pullAcceleration = Vector3.Dot(acceleration, -transform.forward);

            bool success = pullStrength > velocityThreshold && pullAcceleration > accelerationThreshold;
            if (success) Debug.Log($"[FlickGrab] Flick detected! Strength: {pullStrength:F2}, Accel: {pullAcceleration:F2}");
            
            return success;
        }
        
        public Vector3 GetVelocity() => averageVelocity;
    }
}
