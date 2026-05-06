using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FlickGrab
{
    [RequireComponent(typeof(Rigidbody))]
    public class FlickGrabbable : MonoBehaviour
    {
        [Header("Visual Feedback")]
        [SerializeField] private Color highlightColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private float highlightIntensity = 2.5f;
        [SerializeField] private float highlightPulseSpeed = 3f;

        [Header("Flight Settings")]
        [SerializeField] private float flightHeight = 1.5f; // Extra height for the arc
        [SerializeField] private float flightDuration = 0.6f;
        [SerializeField] private AnimationCurve speedCurve = AnimationCurve.Linear(0, 0, 1, 1);

        public event Action<FlickGrabbable> OnArrived;

        private Rigidbody rb;
        private Renderer[] renderers;
        private MaterialPropertyBlock propBlock;
        private Coroutine flightCoroutine;
        private Coroutine pulseCoroutine;
        private bool isHighlighted;
        private XRGrabInteractable grabInteractable;

        public bool IsFlying { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            renderers = GetComponentsInChildren<Renderer>();
            propBlock = new MaterialPropertyBlock();
            grabInteractable = GetComponent<XRGrabInteractable>();
            
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnSelectEntered);
            }
        }

        private void OnDestroy()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            OnGrabbed();
        }

        public void SetHighlight(bool active)
        {
            if (isHighlighted == active) return;
            isHighlighted = active;

            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            if (active) pulseCoroutine = StartCoroutine(PulseRoutine());
            else SetEmission(Color.black);
        }

        public void Launch(Transform target)
        {
            if (IsFlying) return;
            
            SetHighlight(false);
            if (flightCoroutine != null) StopCoroutine(flightCoroutine);
            flightCoroutine = StartCoroutine(FlightRoutine(target));
        }

        private IEnumerator PulseRoutine()
        {
            float time = 0f;
            while (true)
            {
                time += Time.deltaTime * highlightPulseSpeed;
                float pulse = (Mathf.Sin(time) + 1f) * 0.5f;
                Color emissionColor = highlightColor * (highlightIntensity * (0.4f + pulse * 0.6f));
                SetEmission(emissionColor);
                yield return null;
            }
        }

        private IEnumerator FlightRoutine(Transform target)
        {
            IsFlying = true;
            
            // Disable physics during flight
            bool wasKinematic = rb.isKinematic;
            bool usedGravity = rb.useGravity;
            rb.isKinematic = true;
            rb.useGravity = false;

            // Handle colliders - ideally we want to ignore collisions with the player
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.isTrigger = true;

            Vector3 startPos = transform.position;
            float elapsed = 0f;

            // Calculate control point for Bezier arc
            // We want it to go "up" and slightly towards the target
            
            while (elapsed < flightDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flightDuration;
                float curvedT = speedCurve.Evaluate(t);

                // Re-calculate target position in case it moves
                Vector3 currentTargetPos = target.position;
                
                // Calculate an arc point
                // Control point is mid-way but higher
                Vector3 midPoint = (startPos + currentTargetPos) * 0.5f;
                Vector3 controlPoint = midPoint + Vector3.up * flightHeight;

                transform.position = TrajectoryMath.GetBezierPoint(startPos, controlPoint, currentTargetPos, curvedT);
                
                // Satisfying rotation
                transform.Rotate(Vector3.right, 360f * Time.deltaTime);

                yield return null;
            }

            transform.position = target.position;
            
            foreach (var col in colliders) col.isTrigger = false;
            rb.isKinematic = wasKinematic;
            rb.useGravity = usedGravity;
            rb.linearVelocity = Vector3.zero;
            
            IsFlying = false;
            OnArrived?.Invoke(this);
        }

        private void SetEmission(Color color)
        {
            foreach (var rend in renderers)
            {
                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor("_EmissionColor", color);
                rend.SetPropertyBlock(propBlock);
            }
        }
        
        // Disable flight if grabbed manually during flight
        public void OnGrabbed()
        {
            if (IsFlying)
            {
                if (flightCoroutine != null) StopCoroutine(flightCoroutine);
                IsFlying = false;
                // Ensure colliders and rb are restored
                Collider[] colliders = GetComponentsInChildren<Collider>();
                foreach (var col in colliders) col.isTrigger = false;
            }
        }
    }
}
