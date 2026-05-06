using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

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
        [SerializeField] private InputActionReference thumbstickAction;

        [Header("Settings")]
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private float targetingRadius = 0.5f;
        [SerializeField] private LayerMask layerMask = ~0; // Default to Everything
        [SerializeField] private float flickThreshold = -0.5f;

        [Header("Components")]
        [SerializeField] private FlickGestureDetector gestureDetector;
        [SerializeField] private LineRenderer beamLine;

        private FlickGrabbable currentTarget;

        private void Awake()
        {
            Debug.Log($"[FlickGrab] FlickGrabInteractor Awake on {gameObject.name}");
        }

        private void Start()
        {
            Debug.Log($"[FlickGrab] FlickGrabInteractor Start on {gameObject.name}");
            ValidateFields();
        }

        private void ValidateFields()
        {
            if (rayInteractor == null)
            {
                rayInteractor = GetComponent<XRRayInteractor>();
                if (rayInteractor != null) Debug.Log($"[FlickGrab] {gameObject.name}: rayInteractor was null, found on object via GetComponent.");
                else Debug.LogError($"[FlickGrab] {gameObject.name}: rayInteractor is MISSING!");
            }

            if (gestureDetector == null)
            {
                gestureDetector = GetComponent<FlickGestureDetector>();
                if (gestureDetector == null) Debug.LogWarning($"[FlickGrab] {gameObject.name}: gestureDetector is null!");
            }

            if (beamLine == null)
            {
                beamLine = GetComponent<LineRenderer>();
                if (beamLine != null) Debug.Log($"[FlickGrab] {gameObject.name}: beamLine was null, found on object via GetComponent.");
                else Debug.LogError($"[FlickGrab] {gameObject.name}: beamLine is MISSING!");
            }

            if (handAnchor == null)
            {
                // Fallback to RayInteractor's attach transform or transform
                if (rayInteractor != null && rayInteractor.rayOriginTransform != null) handAnchor = rayInteractor.rayOriginTransform;
                else handAnchor = transform;
                Debug.Log($"[FlickGrab] {gameObject.name}: handAnchor is null, using {handAnchor.name}.");
            }
            
            if (thumbstickAction == null || thumbstickAction.action == null) 
            {
                Debug.LogWarning($"[FlickGrab] {gameObject.name}: thumbstickAction is null, joystick flick disabled.");
            }
            else
            {
                Debug.Log($"[FlickGrab] {gameObject.name}: thumbstickAction is VALID ({thumbstickAction.action.name}).");
            }
        }

        private void OnEnable()
        {
            if (thumbstickAction != null && thumbstickAction.action != null)
            {
                thumbstickAction.action.Enable();
            }
        }

        private void OnDisable()
        {
        }

        private void Update()
        {
            PerformRaycast();

            if (Application.isEditor)
            {
                HandleEditorInput();
            }

            if (currentTarget != null && !currentTarget.IsFlying)
            {
                bool triggerFlick = false;

                // Check Joystick
                if (thumbstickAction != null && thumbstickAction.action != null)
                {
                    Vector2 stickValue = thumbstickAction.action.ReadValue<Vector2>();
                    // Allow both horizontal and vertical flick for more leeway in editor/VR
                    if (stickValue.y < flickThreshold || Mathf.Abs(stickValue.x) > 0.7f)
                    {
                        triggerFlick = true;
                    }
                }

                // Check Gesture (keeping it as fallback or secondary)
                if (!triggerFlick && gestureDetector != null && gestureDetector.IsFlicking())
                {
                    triggerFlick = true;
                }

                if (triggerFlick)
                {
                    TriggerFlick();
                }
            }
        }

        private void PerformRaycast()
        {
            Transform origin = transform;
            
            if (rayInteractor != null)
            {
                origin = rayInteractor.transform;
            }

            // In editor fallback
            if (Application.isEditor)
            {
                if (Camera.main != null)
                {
                    if (gameObject == Camera.main.gameObject)
                    {
                        origin = transform;
                    }
                    else if (transform.position.sqrMagnitude < 0.001f)
                    {
                        origin = Camera.main.transform;
                    }
                }
            }

            Ray ray = new Ray(origin.position, origin.forward);
            Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.red);

            // Use SphereCastAll to find all potential targets
            RaycastHit[] hits = Physics.SphereCastAll(ray, targetingRadius, maxDistance, layerMask, QueryTriggerInteraction.Ignore);

            // Sort hits by distance
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            FlickGrabbable foundGrabbable = null;
            Vector3 hitPoint = origin.position + origin.forward * maxDistance;

            foreach (var h in hits)
            {
                // FIND GRABBABLE
                FlickGrabbable grabbable = h.collider.GetComponentInParent<FlickGrabbable>();
                
                // IF NO GRABBABLE, FILTER OUT RIG PARTS
                if (grabbable == null)
                {
                    Transform t = h.transform;
                    bool isSelf = (t == transform || t.IsChildOf(transform) || transform.IsChildOf(t));
                    
                    if (!isSelf && rayInteractor != null)
                    {
                        Transform rt = rayInteractor.transform;
                        if (t == rt || t.IsChildOf(rt) || rt.IsChildOf(t)) isSelf = true;
                    }

                    if (isSelf) continue;

                    // Skip common rig parts that might block the ray at 0m
                    string n = h.collider.name.ToLower();
                    if (n.Contains("xr") || n.Contains("rig") || n.Contains("origin") || n.Contains("hand") || n.Contains("camera") || h.distance < 0.05f)
                        continue;
                    
                    // If we hit some solid environment object (like a wall) BEFORE a grabbable, we should probably stop.
                    // But for now let's keep it simple and see if we can find any grabbable in the sphere.
                    continue; 
                }

                // VALID TARGET FOUND
                if (!grabbable.IsFlying)
                {
                    foundGrabbable = grabbable;
                    // Ensure beam end is at least 0.5m away so it's visible
                    hitPoint = (h.distance > 0.5f) ? h.point : (origin.position + origin.forward * 0.5f);
                    break;
                }
            }
            
            if (foundGrabbable != null)
            {
                if (currentTarget != foundGrabbable)
                {
                    ClearTarget();
                    currentTarget = foundGrabbable;
                    currentTarget.SetHighlight(true);
                }
                UpdateBeam(true, hitPoint);
            }
            else
            {
                if (currentTarget != null)
                {
                    ClearTarget();
                }
                UpdateBeam(false, Vector3.zero);
            }
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
            
            // In many VR setups, the beam should always be visible but change color
            // or we only show it when it hits something. Let's make it always visible
            // to provide feedback that the script is at least trying to draw something.
            beamLine.enabled = true;
            
            Transform origin = transform;
            if (rayInteractor != null)
            {
                origin = rayInteractor.transform;
            }
            
            // In editor, match the raycast origin logic
            if (Application.isEditor)
            {
                if (gameObject == (Camera.main != null ? Camera.main.gameObject : null))
                {
                    origin = transform;
                }
                else if (Camera.main != null && transform.position.sqrMagnitude < 0.001f)
                {
                    origin = Camera.main.transform;
                }
            }

            beamLine.SetPosition(0, origin.position);
            
            if (active)
            {
                beamLine.SetPosition(1, endPoint);
                beamLine.startColor = Color.cyan;
                beamLine.endColor = Color.cyan;
                // Force material color for some shaders
                if (beamLine.material.HasProperty("_BaseColor")) beamLine.material.SetColor("_BaseColor", Color.cyan);
                else if (beamLine.material.HasProperty("_Color")) beamLine.material.SetColor("_Color", Color.cyan);
            }
            else
            {
                beamLine.SetPosition(1, origin.position + origin.forward * maxDistance);
                beamLine.startColor = Color.red; // Red means no target
                beamLine.endColor = new Color(1, 0, 0, 0); // Fade to transparent
                // Force material color
                if (beamLine.material.HasProperty("_BaseColor")) beamLine.material.SetColor("_BaseColor", Color.red);
                else if (beamLine.material.HasProperty("_Color")) beamLine.material.SetColor("_Color", Color.red);
            }
        }

        private void HandleEditorInput()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                {
                    Debug.Log($"[FlickGrab] [Editor] Key Pressed on {gameObject.name} - Simulating Flick");
                    TriggerFlick();
                }
            }
        }
    }
}
