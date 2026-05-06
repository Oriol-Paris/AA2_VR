using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FlickGrab
{
    public class FlickGrabInteractor : MonoBehaviour
    {
        [Header("XRI References")]
        [SerializeField] private XRRayInteractor rayInteractor;
        [SerializeField] private XRDirectInteractor directInteractor;
        [SerializeField] private Transform handAnchor;

        [Header("Input")]
        [SerializeField] private InputActionReference activateAction;

        [Header("Raycast Settings")]
        [SerializeField] private float maxDistance = 10f;
        [SerializeField] private LayerMask layerMask = ~0;

        [Header("Components")]
        [SerializeField] private FlickGestureDetector gestureDetector;
        
        [Header("Visuals")]
        [SerializeField] private LineRenderer beamLine;

        private FlickGrabbable currentTarget;
        private FlickGrabbable lastValidTarget;
        private bool isButtonHeld;
        private float cooldownTimer;
        private const float CooldownTime = 0.5f;

        private void OnEnable()
        {
            if (activateAction != null)
            {
                activateAction.action.Enable();
                activateAction.action.performed += _ => isButtonHeld = true;
                activateAction.action.canceled += _ => { isButtonHeld = false; ClearTarget(); };
            }
        }

        private void OnDisable()
        {
            if (activateAction != null)
            {
                activateAction.action.performed -= _ => isButtonHeld = true;
                activateAction.action.canceled -= _ => { isButtonHeld = false; ClearTarget(); };
            }
        }

        private void Update()
        {
            if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

            if (!isButtonHeld)
            {
                ClearTarget();
                UpdateBeam(false, Vector3.zero);
                return;
            }

            PerformRaycast();

            if (lastValidTarget != null && !lastValidTarget.IsFlying && cooldownTimer <= 0)
            {
                if (gestureDetector.IsFlicking())
                {
                    TriggerFlick();
                }
            }
        }

        private void PerformRaycast()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
            {
                FlickGrabbable grabbable = hit.collider.GetComponentInParent<FlickGrabbable>();
                if (grabbable != null && !grabbable.IsFlying)
                {
                    if (currentTarget != grabbable)
                    {
                        ClearTarget();
                        currentTarget = grabbable;
                        lastValidTarget = grabbable;
                        currentTarget.SetHighlight(true);
                    }
                    UpdateBeam(true, hit.point);
                    return;
                }
            }

            ClearTarget();
            UpdateBeam(false, Vector3.zero);
        }

        private void TriggerFlick()
        {
            if (lastValidTarget == null) return;

            lastValidTarget.OnArrived -= HandleObjectArrival;
            lastValidTarget.OnArrived += HandleObjectArrival;
            
            lastValidTarget.Launch(handAnchor != null ? handAnchor : transform);
            
            cooldownTimer = CooldownTime;
            ClearTarget();
            lastValidTarget = null;
        }

        private void HandleObjectArrival(FlickGrabbable grabbable)
        {
            grabbable.OnArrived -= HandleObjectArrival;

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
