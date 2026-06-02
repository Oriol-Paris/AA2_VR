using UnityEngine;
using UnityEngine.InputSystem;

namespace FlickGrab
{
    public class FlickGrabber : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private string grabbableTag = "Grabbable";
        [SerializeField] private float maxDistance  = 100000f;
        [SerializeField] private LayerMask layerMask    = -1;
        [SerializeField] private InputActionReference grabAction;

        [Header("Flick Detection")]
        [SerializeField] private float flickUpThreshold = 1.5f;
        [SerializeField, Range(0f, 1f)] private float flickDirectionBias = 0.5f;
        [SerializeField] private float aimGracePeriod = 0.6f;

        [Header("Soltar objeto")]
        [SerializeField] private float minHoldTime = 0.3f;

        private enum GrabState
        {
            Idle, Aiming, WaitingForArrival, Held
        }

        private GrabState grabState = GrabState.Idle;
        private IFlickGrabbable currentTarget;
        private GameObject currentTargetObj;
        private IFlickGrabbable heldTarget;
        private float holdTime;
        private bool  isInGrace  = false;
        private float graceTimer = 0f;
        private Vector3 prevPosition;

        private void Awake()
        {
            prevPosition = transform.position;
        }

        private void Update()
        {
            PerformRaycast();
            UpdateGrabState();
            prevPosition = transform.position;
        }

        private void PerformRaycast()
        {
            if (grabState == GrabState.WaitingForArrival ||
                grabState == GrabState.Held) return;

            Ray ray = new Ray(transform.position, transform.forward);
            bool hitCurrentTarget = false;

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
            {
                if (hit.collider.CompareTag(grabbableTag))
                {
                    IFlickGrabbable grabbable = hit.collider.GetComponentInParent<IFlickGrabbable>()
                                             ?? hit.collider.gameObject.AddComponent<FlickGrabbable>();

                    if (grabState == GrabState.Idle)
                    {
                        if (grabbable != null && currentTarget != grabbable)
                        {
                            ClearTarget();
                            currentTarget    = grabbable;
                            currentTargetObj = hit.collider.gameObject;
                            currentTarget.OnPointerEnter();
                        }
                        return;
                    }

                    if (grabState == GrabState.Aiming)
                    {
                        hitCurrentTarget = (grabbable == currentTarget);
                    }
                }
            }

            if (grabState == GrabState.Idle)
            {
                ClearTarget();
            }
            else if (grabState == GrabState.Aiming)
            {
                if (hitCurrentTarget)
                {
                    isInGrace  = false;
                    graceTimer = 0f;
                }
                else if (!isInGrace)
                {
                    isInGrace  = true;
                    graceTimer = 0f;
                    Debug.Log($"[FlickGrab] Rayo fuera del target — grace period ({aimGracePeriod:F1}s).");
                }
            }
        }

        private void UpdateGrabState()
        {
            if (grabAction == null) return;

            bool triggerPressed  = grabAction.action.WasPressedThisFrame();
            bool triggerHeld     = grabAction.action.IsPressed();
            bool triggerReleased = grabAction.action.WasReleasedThisFrame();

            Vector3 velocity = (transform.position - prevPosition) / Time.deltaTime;

            switch (grabState)
            {
                case GrabState.Idle:
                    if (triggerPressed && currentTarget != null)
                    {
                        grabState  = GrabState.Aiming;
                        isInGrace  = false;
                        graceTimer = 0f;
                        currentTarget.OnAimStart();
                        Debug.Log("[FlickGrab] Aiming — haz un flick hacia arriba.");
                    }
                    break;
                case GrabState.Aiming:
                    if (triggerReleased || !triggerHeld)
                    {
                        CancelAim();
                        ClearTarget();
                        break;
                    }
                    if (currentTarget == null)
                    {
                        isInGrace  = false;
                        graceTimer = 0f;
                        grabState  = GrabState.Idle;
                        break;
                    }
                    if (isInGrace)
                    {
                        graceTimer += Time.deltaTime;
                        if (graceTimer >= aimGracePeriod)
                        {
                            Debug.Log("[FlickGrab] Grace period expirado — aim cancelado.");
                            CancelAim();
                            ClearTarget();
                            break;
                        }
                    }
                    float upSpeed    = Vector3.Dot(velocity, Vector3.up);
                    float totalSpeed = velocity.magnitude;
                    float upRatio    = totalSpeed > 0.01f ? upSpeed / totalSpeed : 0f;

                    if (upSpeed >= flickUpThreshold && upRatio >= flickDirectionBias)
                    {
                        Debug.Log($"[FlickGrab] ¡Flick! vel↑={upSpeed:F2} m/s, ratio={upRatio:F2}");

                        isInGrace  = false;
                        graceTimer = 0f;

                        heldTarget       = currentTarget;
                        currentTarget    = null;
                        currentTargetObj = null;
                        grabState        = GrabState.WaitingForArrival;

                        heldTarget.OnFlickGrab(transform);
                    }
                    break;
                case GrabState.WaitingForArrival:
                    if (heldTarget == null || (heldTarget as MonoBehaviour) == null)
                    {
                        heldTarget = null;
                        grabState  = GrabState.Idle;
                        break;
                    }

                    if (heldTarget.IsInHand)
                    {
                        grabState = GrabState.Held;
                        holdTime  = 0f;
                        Debug.Log("[FlickGrab] Objeto en mano — suelta el gatillo para soltarlo.");
                    }
                    break;
                case GrabState.Held:
                    if (heldTarget == null || (heldTarget as MonoBehaviour) == null)
                    {
                        heldTarget = null;
                        grabState  = GrabState.Idle;
                        break;
                    }

                    holdTime += Time.deltaTime;
                    if (holdTime >= minHoldTime && triggerReleased)
                    {
                        Vector3 throwVelocity = (transform.position - prevPosition) / Time.deltaTime;
                        heldTarget.Release(throwVelocity);
                        heldTarget = null;
                        grabState  = GrabState.Idle;
                        Debug.Log("[FlickGrab] Objeto soltado.");
                    }
                    break;
            }
        }

        private void CancelAim()
        {
            isInGrace  = false;
            graceTimer = 0f;
            currentTarget?.OnAimCancel();
            grabState = GrabState.Idle;
            Debug.Log("[FlickGrab] Aim cancelado.");
        }

        private void ClearTarget()
        {
            if (currentTarget != null)
            {
                currentTarget.OnPointerExit();
                currentTarget    = null;
                currentTargetObj = null;
            }
        }

        private void OnDisable()
        {
            isInGrace  = false;
            graceTimer = 0f;

            if (grabState == GrabState.Aiming)
                CancelAim();

            if (grabState == GrabState.Held && heldTarget != null)
            {
                heldTarget.Release(Vector3.zero);
                heldTarget = null;
            }

            ClearTarget();
            grabState = GrabState.Idle;
        }

    }
}