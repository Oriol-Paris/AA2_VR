using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FlickGrab
{
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class FlickGrabbable : MonoBehaviour
    {
        [Header("Flight Settings")]
        [SerializeField] private float flightHeight = 1.0f;
        [SerializeField] private float flightDuration = 0.5f;
        [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Visual Feedback")]
        [SerializeField] private Color highlightColor = Color.cyan;

        public event Action<FlickGrabbable> OnArrived;
        public bool IsFlying { get; private set; }

        private Rigidbody rb;
        private XRGrabInteractable grabInteractable;
        private Renderer[] renderers;
        private MaterialPropertyBlock propBlock;
        private Coroutine flightCoroutine;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            grabInteractable = GetComponent<XRGrabInteractable>();
            renderers = GetComponentsInChildren<Renderer>();
            propBlock = new MaterialPropertyBlock();
            
            grabInteractable.selectEntered.AddListener(OnSelected);
        }

        private void OnDestroy()
        {
            if (grabInteractable != null)
                grabInteractable.selectEntered.RemoveListener(OnSelected);
        }

        private void OnSelected(SelectEnterEventArgs args) => StopFlight();

        public void SetHighlight(bool active)
        {
            // Simple emission toggle
            Color color = active ? highlightColor * 2f : Color.black;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(propBlock);
                propBlock.SetColor("_EmissionColor", color);
                r.SetPropertyBlock(propBlock);
                
                // Ensure emission keyword is enabled on the material if needed
                if (active)
                {
                    foreach (var mat in r.materials)
                    {
                        mat.EnableKeyword("_EMISSION");
                    }
                }
            }
        }

        public void Launch(Transform target)
        {
            if (IsFlying) return;
            StopFlight();
            flightCoroutine = StartCoroutine(FlightRoutine(target));
        }

        private void StopFlight()
        {
            if (flightCoroutine != null) StopCoroutine(flightCoroutine);
            IsFlying = false;
        }

        private IEnumerator FlightRoutine(Transform target)
        {
            IsFlying = true;
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true;

            Vector3 startPos = transform.position;
            float elapsed = 0f;

            while (elapsed < flightDuration)
            {
                elapsed += Time.deltaTime;
                float t = speedCurve.Evaluate(elapsed / flightDuration);
                
                Vector3 endPos = target.position;
                Vector3 midPoint = (startPos + endPos) * 0.5f + Vector3.up * flightHeight;

                transform.position = TrajectoryMath.GetBezierPoint(startPos, midPoint, endPos, t);
                transform.Rotate(Vector3.right, 360f * Time.deltaTime);

                yield return null;
            }

            transform.position = target.position;
            rb.isKinematic = wasKinematic;
            IsFlying = false;
            
            OnArrived?.Invoke(this);
        }
    }
}
