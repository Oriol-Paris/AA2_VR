using UnityEngine;
using System.Collections;

namespace FlickGrab
{
    /// <summary>
    /// Default component for grabbable objects
    /// Manages visual feedback and object attraction
    /// </summary>
    public class FlickGrabbable : MonoBehaviour, IFlickGrabbable
    {
        [Header("Feedback")]
        [SerializeField] private Color highlightColor = Color.cyan;
        private Color originalColor;
        private Renderer objectRenderer;
        private MaterialPropertyBlock propBlock;
        private bool isHighlighted = false;

        [Header("Movement")]
        [SerializeField] private float travelDuration = 0.6f;
        [SerializeField] private float arcHeight = 0.5f;
        
        private bool isMoving = false;
        private Rigidbody rb;

        private void Awake()
        {
            objectRenderer = GetComponent<Renderer>();
            rb = GetComponent<Rigidbody>();
            propBlock = new MaterialPropertyBlock();
            
            if (objectRenderer != null)
            {
                if (objectRenderer.sharedMaterial.HasProperty("_BaseColor"))
                    originalColor = objectRenderer.sharedMaterial.GetColor("_BaseColor");
                else if (objectRenderer.sharedMaterial.HasProperty("_Color"))
                    originalColor = objectRenderer.sharedMaterial.color;
                else
                    originalColor = Color.white;
            }
        }

        public void OnPointerEnter()
        {
            if (isMoving) return;
            ApplyHighlight(true);
        }

        public void OnPointerExit()
        {
            ApplyHighlight(false);
        }

        public void OnFlickGrab(Transform handTransform)
        {
            if (isMoving) return;
            StartCoroutine(MoveInArcCoroutine(handTransform));
        }

        private void ApplyHighlight(bool highlight)
        {
            if (objectRenderer == null) return;
            
            isHighlighted = highlight;
            objectRenderer.GetPropertyBlock(propBlock);
            
            Color targetColor = highlight ? highlightColor : originalColor;
            
            if (objectRenderer.sharedMaterial.HasProperty("_BaseColor"))
                propBlock.SetColor("_BaseColor", targetColor);
            else if (objectRenderer.sharedMaterial.HasProperty("_Color"))
                propBlock.SetColor("_Color", targetColor);
            
            objectRenderer.SetPropertyBlock(propBlock);
        }

        private IEnumerator MoveInArcCoroutine(Transform target)
        {
            isMoving = true;
            ApplyHighlight(false);

            bool wasKinematic = false;
            if (rb != null)
            {
                wasKinematic = rb.isKinematic;
                rb.isKinematic = true;
            }

            Vector3 startPos = transform.position;
            float elapsed = 0;

            while (elapsed < travelDuration)
            {
                if (target == null) break;

                elapsed += Time.deltaTime;
                float t = elapsed / travelDuration;
                
                float easedT = FlickGrabUtils.SmoothStep(t);
                transform.position = FlickGrabUtils.GetParabolicPoint(startPos, target.position, arcHeight, easedT);
                yield return null;
            }

            if (target != null)
                transform.position = target.position;

            if (rb != null)
            {
                rb.isKinematic = wasKinematic;
            }

            isMoving = false;
        }
    }
}
