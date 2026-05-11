using UnityEngine;
using UnityEngine.InputSystem;

namespace FlickGrab
{
    /// <summary>
    /// Component that detects and attracts objects
    /// Has to be added to each controller/hand
    /// </summary>
    public class FlickGrabber : MonoBehaviour
    {
        [SerializeField] private string grabbableTag = "Grabbable";
        [SerializeField] private float maxDistance = 100000f;
        [SerializeField] private LayerMask layerMask = -1;
        [SerializeField] private InputActionReference grabAction;

        private IFlickGrabbable currentTarget;
        private GameObject currentTargetObj;

        private void Update()
        {
            PerformRaycast();
            CheckInput();
        }

        /// <summary>
        /// Throws a Raycast to detect objects with the desired tag
        /// </summary>
        private void PerformRaycast()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
            {
                if (hit.collider.CompareTag(grabbableTag))
                {
                    hit.collider.GetComponent<MeshRenderer>().material.color = Color.blue;
                    
                    /*IFlickGrabbable grabbable = hit.collider.GetComponentInParent<IFlickGrabbable>();
                    
                    if (grabbable == null)
                    {
                        grabbable = hit.collider.gameObject.AddComponent<FlickGrabbable>();
                    }

                    if (grabbable != null)
                    {
                        if (currentTarget != grabbable)
                        {
                            ClearTarget();
                            currentTarget = grabbable;
                            currentTargetObj = hit.collider.gameObject;
                            currentTarget.OnPointerEnter();
                        }
                        return;
                    }*/
                }
            }

            ClearTarget();
        }

        /// <summary>
        /// Clears current target and disables visual feedback
        /// </summary>
        private void ClearTarget()
        {
            if (currentTarget != null)
            {
                currentTarget.OnPointerExit();
                currentTarget = null;
                currentTargetObj = null;
            }
        }

        /// <summary>
        /// Checks if the desired input was pressed
        /// </summary>
        private void CheckInput()
        {
            if (grabAction == null || currentTarget == null)
            {
                Debug.Log("Grab action null.");
                return;
            }

            if (grabAction.action.WasPressedThisFrame())
            {
                Debug.Log("Grab action used.");
                currentTarget.OnFlickGrab(this.transform);
            }
        }

        private void OnDisable()
        {
            ClearTarget();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = currentTarget != null ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position, transform.forward * maxDistance);
        }
    }
}
