using UnityEngine;
using System.Collections;

namespace FlickGrab
{
    public class FlickGrabbable : MonoBehaviour, IFlickGrabbable
    {
        [Header("Feedback visual")]
        [SerializeField] private Color highlightColor = Color.cyan;
        [SerializeField] private Color aimColor = new Color(1f, 0.55f, 0f);
        [SerializeField] private float aimPulseSpeed = 5f;

        [Header("Movimiento")]
        [SerializeField] private float travelDuration = 0.6f;
        [SerializeField] private float arcHeight = 0.5f;

        [Header("En mano")]
        [SerializeField] private Vector3 handPositionOffset = Vector3.zero;
        [SerializeField] private Vector3 handRotationOffset = Vector3.zero;

        private Color originalColor;
        private Renderer objectRenderer;
        private MaterialPropertyBlock propBlock;
        private Rigidbody rb;
        private bool wasKinematic;

        private bool isMoving  = false;
        private bool isHovered = false;
        private bool isAiming  = false;
        private bool isInHand  = false;

        private Coroutine aimPulseCoroutine;
        public bool IsInHand => isInHand;

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

        private void OnDestroy()
        {
            isInHand = false;
            StopAimPulse();
        }

        public void OnPointerEnter()
        {
            if (isMoving || isAiming || isInHand) return;
            isHovered = true;
            StopAimPulse();
            SetColor(highlightColor);
        }
        
        public void OnPointerExit()
        {
            isHovered = false;
            isAiming  = false;
            StopAimPulse();
            SetColor(originalColor);
        }
        
        public void OnAimStart()
        {
            if (isMoving || isInHand) return;
            isAiming = true;
            StopAimPulse();
            aimPulseCoroutine = StartCoroutine(AimPulseLoop());
        }

        public void OnAimCancel()
        {
            isAiming = false;
            StopAimPulse();
            SetColor(isHovered ? highlightColor : originalColor);
        }
        
        public void OnFlickGrab(Transform handTransform)
        {
            if (isMoving || isInHand) return;
            isAiming  = false;
            isHovered = false;
            StopAimPulse();
            StartCoroutine(MoveInArcCoroutine(handTransform));
        }

        public void Release(Vector3 throwVelocity)
        {
            if (!isInHand) return;

            transform.SetParent(null);
            isInHand = false;

            if (rb != null)
            {
                rb.isKinematic = wasKinematic;

                if (!wasKinematic)
                    rb.linearVelocity = throwVelocity;
            }

            Debug.Log($"[FlickGrab] {name} liberado — vel={throwVelocity.magnitude:F2} m/s");
        }

        private void SetColor(Color color)
        {
            if (objectRenderer == null) return;
            objectRenderer.GetPropertyBlock(propBlock);

            if (objectRenderer.sharedMaterial.HasProperty("_BaseColor"))
                propBlock.SetColor("_BaseColor", color);
            
            else if (objectRenderer.sharedMaterial.HasProperty("_Color"))
                propBlock.SetColor("_Color", color);

            objectRenderer.SetPropertyBlock(propBlock);
        }

        private IEnumerator AimPulseLoop()
        {
            float t = 0f;
            
            while (true)
            {
                t += Time.deltaTime * aimPulseSpeed;
                float pulse = (Mathf.Sin(t) + 1f) * 0.5f;
                SetColor(Color.Lerp(highlightColor, aimColor, pulse));
                yield return null;
            }
        }

        private void StopAimPulse()
        {
            if (aimPulseCoroutine != null)
            {
                StopCoroutine(aimPulseCoroutine);
                aimPulseCoroutine = null;
            }
        }

        private IEnumerator MoveInArcCoroutine(Transform target)
        {
            isMoving = true;
            SetColor(originalColor);

            if (rb != null)
            {
                wasKinematic = rb.isKinematic;
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Vector3 startPos = transform.position;
            float   elapsed  = 0f;

            while (elapsed < travelDuration)
            {
                if (target == null) break;

                elapsed += Time.deltaTime;
                float t      = elapsed / travelDuration;
                float easedT = FlickGrabUtils.SmoothStep(t);
                transform.position = FlickGrabUtils.GetParabolicPoint(startPos, target.position, arcHeight, easedT);
                yield return null;
            }

            isMoving = false;

            if (target != null)
            {
                transform.SetParent(target);
                transform.localPosition = handPositionOffset;
                transform.localRotation = Quaternion.Euler(handRotationOffset);
                isInHand = true;

                Debug.Log($"[FlickGrab] {name} llegó a la mano — suelta el gatillo para soltarlo.");
            }
            else
            {
                if (rb != null)
                    rb.isKinematic = wasKinematic;

                Debug.LogWarning($"[FlickGrab] {name}: target destruido en vuelo, objeto suelto.");
            }
        }
    }
}