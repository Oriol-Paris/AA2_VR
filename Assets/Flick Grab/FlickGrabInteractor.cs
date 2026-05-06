using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FlickGrab
{
    /// <summary>
    /// Manages the flick interaction from the hand.
    /// </summary>
    public class FlickGrabInteractor : MonoBehaviour
    {
        [Header("XRI References")]
        [SerializeField] private XRRayInteractor rayInteractor;
        [SerializeField] private XRDirectInteractor directInteractor;
        [SerializeField] private Transform handAnchor;

        [Header("Input")]
        [SerializeField] private InputActionReference activateAction;

        [Header("Settings")]
        [SerializeField] private float maxDistance = 10f;
        [SerializeField] private LayerMask layerMask = -1;

        [Header("Components")]
        [SerializeField] private FlickGestureDetector gestureDetector;
        [SerializeField] private LineRenderer beamLine;

        private FlickGrabbable currentTarget;
        private bool isButtonHeld;

        private void OnEnable()
        {
            if (activateAction != null)
            {
                activateAction.action.Enable();
                activateAction.action.performed += OnButtonPressed;
                activateAction.action.canceled += OnButtonReleased;
            }
            else
            {
                Debug.LogWarning("[FlickGrab] Activate Action is not assigned on FlickGrabInteractor!");
            }
        }

        private void OnDisable()
        {
            if (activateAction != null)
            {
                activateAction.action.performed -= OnButtonPressed;
                activateAction.action.canceled -= OnButtonReleased;
            }
        }

        private void OnButtonPressed(InputAction.CallbackContext context)
        {
            isButtonHeld = true;
            Debug.Log("[FlickGrab] Button Pressed");
        }

        private void OnButtonReleased(InputAction.CallbackContext context)
        {
            isButtonHeld = false;
            Debug.Log("[FlickGrab] Button Released");
        }

        private void Update()
        {
            if (!isButtonHeld)
            {
                if (currentTarget != null) Debug.Log("[FlickGrab] Button released, clearing target.");
                ClearTarget();
                UpdateBeam(false, Vector3.zero);
                return;
            }

            PerformRaycast();

            if (currentTarget != null && !currentTarget.IsFlying)
            {
                if (gestureDetector != null && gestureDetector.IsFlicking())
                {
                    Debug.Log("[FlickGrab] Flick gesture detected!");
                    TriggerFlick();
                }
            }
        }

        private void PerformRaycast()
        {
            // Use the interactor's forward as the ray direction
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
            {
                FlickGrabbable grabbable = hit.collider.GetComponentInParent<FlickGrabbable>();
                if (grabbable != null && !grabbable.IsFlying)
                {
                    if (currentTarget != grabbable)
                    {
                        Debug.Log($"[FlickGrab] Raycast hit new target: {grabbable.name}");
                        ClearTarget();
                        currentTarget = grabbable;
                        currentTarget.SetHighlight(true);
                    }
                    UpdateBeam(true, hit.point);
                    return;
                }
            }

            if (currentTarget != null)
            {
                Debug.Log("[FlickGrab] Raycast lost target.");
                ClearTarget();
            }
            UpdateBeam(false, Vector3.zero);
        }

        private void TriggerFlick()
        {
            if (currentTarget == null) return;

            FlickGrabbable launchingTarget = currentTarget;
            launchingTarget.OnArrived += HandleArrival;
            launchingTarget.Launch(handAnchor != null ? handAnchor : transform);
            
            ClearTarget();
        }

        private void HandleArrival(FlickGrabbable grabbable)
        {
            grabbable.OnArrived -= HandleArrival;

            if (directInteractor != null)
            {
                IXRSelectInteractable interactable = grabbable.GetComponent<XRGrabInteractable>();
                if (interactable != null)
                {
                    directInteractor.StartManualInteraction(interactable);
                }
            }
        }

        private void ClearTarget()
        {
            if (currentTarget != null)
            {
                currentTarget.SetHighlight(false);
                currentTarget = null;
            }
        }

        private void UpdateBeam(bool active, Vector3 endPoint)
        {
            if (beamLine == null) return;
            beamLine.enabled = active;
            if (active)
            {
                beamLine.SetPosition(0, transform.position);
                beamLine.SetPosition(1, endPoint);
            }
        }
    }
}
