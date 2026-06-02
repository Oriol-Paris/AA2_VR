using UnityEngine;
using System.Collections;

namespace FlickGrab
{
    /// <summary>
    /// Componente por defecto para objetos cogibles con FlickGrab.
    ///
    /// Estados visuales:
    ///   Normal   → color original.
    ///   Hover    → cian (el rayo está encima).
    ///   Aiming   → pulso cian ↔ naranja ("¡hazme flick!").
    ///   En mano  → color original, sin highlight.
    ///
    /// Al llegar a la mano el objeto se parentea al mando y permanece ahí
    /// hasta que FlickGrabber llame a Release().
    /// </summary>
    public class FlickGrabbable : MonoBehaviour, IFlickGrabbable
    {
        // ═══════════════════════════════════════════════
        //  INSPECTOR
        // ═══════════════════════════════════════════════

        [Header("─── Feedback visual ───")]
        [Tooltip("Color cuando el rayo apunta al objeto (hover).")]
        [SerializeField] private Color highlightColor = Color.cyan;

        [Tooltip("Color de destino del pulso cuando el jugador mantiene el gatillo (aiming).")]
        [SerializeField] private Color aimColor = new Color(1f, 0.55f, 0f);   // naranja

        [Tooltip("Velocidad del pulso en ciclos por segundo.")]
        [SerializeField] private float aimPulseSpeed = 5f;

        [Header("─── Movimiento ───")]
        [Tooltip("Duración del vuelo hacia la mano en segundos.")]
        [SerializeField] private float travelDuration = 0.6f;

        [Tooltip("Altura máxima del arco parabólico.")]
        [SerializeField] private float arcHeight = 0.5f;

        [Header("─── En mano ───")]
        [Tooltip("Offset de posición local respecto al mando cuando está cogido.")]
        [SerializeField] private Vector3 handPositionOffset = Vector3.zero;

        [Tooltip("Offset de rotación local respecto al mando cuando está cogido.")]
        [SerializeField] private Vector3 handRotationOffset = Vector3.zero;

        // ═══════════════════════════════════════════════
        //  ESTADO INTERNO
        // ═══════════════════════════════════════════════

        private Color                 originalColor;
        private Renderer              objectRenderer;
        private MaterialPropertyBlock propBlock;
        private Rigidbody             rb;
        private bool                  wasKinematic;   // estado de Rigidbody antes del snap

        private bool     isMoving  = false;
        private bool     isHovered = false;
        private bool     isAiming  = false;
        private bool     isInHand  = false;

        private Coroutine aimPulseCoroutine;

        // ═══════════════════════════════════════════════
        //  IFlickGrabbable — propiedad
        // ═══════════════════════════════════════════════

        public bool IsInHand => isInHand;

        // ═══════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ═══════════════════════════════════════════════

        private void Awake()
        {
            objectRenderer = GetComponent<Renderer>();
            rb             = GetComponent<Rigidbody>();
            propBlock      = new MaterialPropertyBlock();

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
            // Limpiar estado para que FlickGrabber detecte el null correctamente
            isInHand = false;
            StopAimPulse();
        }

        // ═══════════════════════════════════════════════
        //  IFlickGrabbable — eventos
        // ═══════════════════════════════════════════════

        /// <summary>El rayo entra al objeto → resaltado cian.</summary>
        public void OnPointerEnter()
        {
            if (isMoving || isAiming || isInHand) return;
            isHovered = true;
            StopAimPulse();
            SetColor(highlightColor);
        }

        /// <summary>El rayo sale del objeto → vuelve al color original.</summary>
        public void OnPointerExit()
        {
            isHovered = false;
            isAiming  = false;
            StopAimPulse();
            SetColor(originalColor);
        }

        /// <summary>
        /// Gatillo mantenido con el rayo en el objeto.
        /// Inicia el pulso naranja/cian para indicar "listo para flick".
        /// </summary>
        public void OnAimStart()
        {
            if (isMoving || isInHand) return;
            isAiming = true;
            StopAimPulse();
            aimPulseCoroutine = StartCoroutine(AimPulseLoop());
        }

        /// <summary>
        /// El aim se canceló (gatillo suelto o rayo fuera del objeto).
        /// Vuelve al estado de hover si el rayo todavía apunta al objeto.
        /// </summary>
        public void OnAimCancel()
        {
            isAiming = false;
            StopAimPulse();
            // Si el rayo sigue sobre el objeto → volver a cian; si no → color original.
            SetColor(isHovered ? highlightColor : originalColor);
        }

        /// <summary>
        /// Flick confirmado. Empieza a volar hacia el mando.
        /// Al llegar se parentea al mando y activa IsInHand.
        /// </summary>
        public void OnFlickGrab(Transform handTransform)
        {
            if (isMoving || isInHand) return;
            isAiming  = false;
            isHovered = false;
            StopAimPulse();
            StartCoroutine(MoveInArcCoroutine(handTransform));
        }

        /// <summary>
        /// Llamado por FlickGrabber cuando el jugador suelta el gatillo.
        /// Desparentea el objeto, restaura la física y aplica velocidad de lanzamiento.
        /// </summary>
        public void Release(Vector3 throwVelocity)
        {
            if (!isInHand) return;

            transform.SetParent(null);
            isInHand = false;

            if (rb != null)
            {
                rb.isKinematic = wasKinematic;

                // Solo añadir velocidad si la física está activa
                if (!wasKinematic)
                    rb.linearVelocity = throwVelocity;
            }

            Debug.Log($"[FlickGrab] {name} liberado — vel={throwVelocity.magnitude:F2} m/s");
        }

        // ═══════════════════════════════════════════════
        //  VISUAL
        // ═══════════════════════════════════════════════

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

        /// <summary>Pulso cian ↔ naranja mientras el jugador mantiene el gatillo.</summary>
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

        // ═══════════════════════════════════════════════
        //  MOVIMIENTO
        // ═══════════════════════════════════════════════

        private IEnumerator MoveInArcCoroutine(Transform target)
        {
            isMoving = true;
            SetColor(originalColor);

            // Congelar física durante el vuelo
            if (rb != null)
            {
                wasKinematic       = rb.isKinematic;
                rb.isKinematic     = true;
                rb.linearVelocity  = Vector3.zero;
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
                // ── Anclar a la mano ──────────────────────────────────────────
                // Parentar al mando y aplicar offsets configurables.
                // La física permanece kinematic mientras está en mano.
                transform.SetParent(target);
                transform.localPosition = handPositionOffset;
                transform.localRotation = Quaternion.Euler(handRotationOffset);
                isInHand = true;

                Debug.Log($"[FlickGrab] {name} llegó a la mano — suelta el gatillo para soltarlo.");
            }
            else
            {
                // El target fue destruido en vuelo → restaurar física y dejar caer.
                if (rb != null)
                    rb.isKinematic = wasKinematic;

                Debug.LogWarning($"[FlickGrab] {name}: target destruido en vuelo, objeto suelto.");
            }
        }
    }
}
