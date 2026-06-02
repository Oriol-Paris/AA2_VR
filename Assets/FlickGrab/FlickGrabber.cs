using UnityEngine;
using UnityEngine.InputSystem;

namespace FlickGrab
{
    /// <summary>
    /// Detecta objetos con un rayo y los atrae con un gesto de flick (estilo Half-Life: Alyx).
    ///
    /// FLUJO:
    ///   1. Apunta al objeto          → se ilumina en cian.
    ///   2. Mantén el gatillo         → pulso naranja/cian ("¡hazme flick!").
    ///   3. Mueve el mando hacia arriba rápido → objeto vuela a tu mano.
    ///   4. Objeto en mano            → suelta el gatillo para tirarlo.
    ///
    ///   Si dejas de apuntar al objeto (mientras mantienes el gatillo) → se cancela de inmediato.
    ///   Si sueltas el gatillo antes de hacer el flick → se cancela.
    ///
    /// SETUP:
    ///   Añade este componente a cada mando. Asigna grabAction (el gatillo).
    ///   Los objetos deben tener el tag indicado y el componente FlickGrabbable.
    /// </summary>
    public class FlickGrabber : MonoBehaviour
    {
        // ═══════════════════════════════════════════════
        //  INSPECTOR
        // ═══════════════════════════════════════════════

        [Header("─── Raycast ───")]
        [SerializeField] private string    grabbableTag = "Grabbable";
        [SerializeField] private float     maxDistance  = 100000f;
        [SerializeField] private LayerMask layerMask    = -1;
        [SerializeField] private InputActionReference grabAction;

        [Header("─── Flick Detection ───")]
        [Tooltip("Velocidad mínima hacia arriba (m/s) para confirmar el flick. Rango recomendado: 1.0 – 2.5.")]
        [SerializeField] private float flickUpThreshold = 1.5f;

        [Tooltip("Fracción mínima del movimiento que debe ser hacia arriba (0 = cualquier dirección, 1 = solo vertical).")]
        [SerializeField, Range(0f, 1f)] private float flickDirectionBias = 0.5f;

        [Tooltip("Segundos de margen tras dejar de apuntar al objeto para seguir pudiendo hacer el flick.\n" +
                 "Durante esta ventana el objeto sigue pulsando y el gesto sigue siendo válido.")]
        [SerializeField] private float aimGracePeriod = 0.6f;

        [Header("─── Soltar objeto ───")]
        [Tooltip("Tiempo mínimo (s) que el objeto debe estar en la mano antes de poder soltarlo. Evita sueltas accidentales.")]
        [SerializeField] private float minHoldTime = 0.3f;

        // ═══════════════════════════════════════════════
        //  ESTADO INTERNO
        // ═══════════════════════════════════════════════

        private enum GrabState
        {
            Idle,               // Sin acción. Buscando targets.
            Aiming,             // Gatillo pulsado sobre target. Esperando flick.
            WaitingForArrival,  // Flick confirmado. Objeto volando.
            Held                // Objeto en mano. Esperando que el jugador lo suelte.
        }

        private GrabState       grabState = GrabState.Idle;
        private IFlickGrabbable currentTarget;
        private GameObject      currentTargetObj;
        private IFlickGrabbable heldTarget;
        private float           holdTime;

        // Grace period: ventana de tiempo tras perder el rayo donde el flick sigue siendo válido
        private bool  isInGrace  = false;
        private float graceTimer = 0f;

        private Vector3 prevPosition;

        // ═══════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ═══════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════
        //  RAYCAST
        // ═══════════════════════════════════════════════

        private void PerformRaycast()
        {
            // El objeto ya está en mano o siendo cogido: no cambiar targets.
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
                        // Cambiar highlight si el target cambió
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
                        // Solo interesa saber si seguimos apuntando al mismo objeto
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
                    // El rayo volvió al target → resetear grace period.
                    isInGrace  = false;
                    graceTimer = 0f;
                }
                else if (!isInGrace)
                {
                    // El rayo acaba de salir del target → iniciar ventana de gracia.
                    isInGrace  = true;
                    graceTimer = 0f;
                    Debug.Log($"[FlickGrab] Rayo fuera del target — grace period ({aimGracePeriod:F1}s).");
                }
                // Si ya estaba en grace, no hacer nada: el timer avanza en UpdateGrabState.
            }
        }

        // ═══════════════════════════════════════════════
        //  MÁQUINA DE ESTADOS
        // ═══════════════════════════════════════════════

        private void UpdateGrabState()
        {
            if (grabAction == null) return;

            bool triggerPressed  = grabAction.action.WasPressedThisFrame();
            bool triggerHeld     = grabAction.action.IsPressed();
            bool triggerReleased = grabAction.action.WasReleasedThisFrame();

            // Velocidad del mando este frame (espacio de mundo)
            Vector3 velocity = (transform.position - prevPosition) / Time.deltaTime;

            switch (grabState)
            {
                // ── Idle ──────────────────────────────────────────────────────────
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

                // ── Aiming ───────────────────────────────────────────────────────
                // Gatillo mantenido. Esperar gesto de flick.
                // Si el rayo pierde el target, hay una ventana de gracia (aimGracePeriod)
                // durante la cual el flick sigue siendo válido.
                case GrabState.Aiming:
                    // Cancelar si se suelta el gatillo
                    if (triggerReleased || !triggerHeld)
                    {
                        CancelAim();
                        ClearTarget();
                        break;
                    }

                    // Cancelar si el target fue destruido
                    if (currentTarget == null)
                    {
                        isInGrace  = false;
                        graceTimer = 0f;
                        grabState  = GrabState.Idle;
                        break;
                    }

                    // ── Grace period ─────────────────────────────────────────────
                    // El rayo salió del objeto pero la ventana de tiempo sigue abierta.
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

                    // ── Detección del flick ──────────────────────────────────────
                    // Funciona tanto si el rayo está en el objeto como en grace period.
                    // upSpeed  = componente de velocidad en el eje Y global
                    // upRatio  = fracción del movimiento que va hacia arriba
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

                // ── WaitingForArrival ─────────────────────────────────────────────
                // El objeto está volando hacia la mano. Esperar a que llegue.
                case GrabState.WaitingForArrival:
                    // Protección por si el objeto fue destruido en vuelo
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

                // ── Held ─────────────────────────────────────────────────────────
                // El objeto está en la mano. Soltar al liberar el gatillo.
                case GrabState.Held:
                    if (heldTarget == null || (heldTarget as MonoBehaviour) == null)
                    {
                        heldTarget = null;
                        grabState  = GrabState.Idle;
                        break;
                    }

                    holdTime += Time.deltaTime;

                    // Permitir soltar solo después del tiempo mínimo
                    // (evita sueltas accidentales justo al llegar a la mano)
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

        // ═══════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════
        //  GIZMOS
        // ═══════════════════════════════════════════════

        private void OnDrawGizmos()
        {
            // Rojo=sin target | Verde=hover | Amarillo=aiming | Magenta=volando | Blanco=en mano
            Gizmos.color = grabState == GrabState.Aiming           ? Color.yellow  :
                           grabState == GrabState.WaitingForArrival ? Color.magenta :
                           grabState == GrabState.Held              ? Color.white   :
                           currentTarget != null                    ? Color.green   : Color.red;

            float gizmoLen = Mathf.Min(maxDistance, 10f);
            Gizmos.DrawRay(transform.position, transform.forward * gizmoLen);
        }
    }
}
