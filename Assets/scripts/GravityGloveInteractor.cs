using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// Sistema de Gravity Glove inspirado en Half-Life: Alyx.
/// 
/// SETUP:
///  1. Añade este script al GameObject del mando/mano (el que tiene el XRRayInteractor).
///  2. Rellena las referencias en el Inspector (ver región INSPECTOR).
///  3. Añade GravityGlovable a los objetos que quieras atraer.
///  4. Asegúrate de que esos objetos tienen su material con Emission habilitado.
/// </summary>
public class GravityGloveInteractor : MonoBehaviour
{
    // ═══════════════════════════════════════════════
    //  INSPECTOR
    // ═══════════════════════════════════════════════

    [Header("─── Referencias XR ───")]
    [Tooltip("El XRRayInteractor de este mando (para el raycast).")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;

    [Tooltip("El XRDirectInteractor de este mando (para el auto-grab al llegar).")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor directInteractor;

    [Tooltip("Transform exacto de la palma/mano. El objeto vuela hacia aquí.")]
    [SerializeField] private Transform handAnchor;

    [Header("─── Input ───")]
    [Tooltip("Botón que hay que mantener pulsado para activar el Gravity Glove (p.ej. Grip).")]
    [SerializeField] private InputActionReference activateAction;

    [Header("─── Raycast ───")]
    [Tooltip("Distancia máxima del raycast.")]
    [SerializeField] private float maxRayDistance = 10f;

    [Tooltip("Layers en las que busca objetos atraibles.")]
    [SerializeField] private LayerMask attractableLayer = ~0; // Todo por defecto

    [Header("─── Detección del Gesto ───")]
    [Tooltip("Velocidad mínima del mando hacia atrás (m/s) para disparar la atracción.")]
    [SerializeField] private float flickThreshold = 1.6f;

    [Tooltip("Segundos de espera entre atracciones consecutivas.")]
    [SerializeField] private float attractCooldown = 0.5f;

    [Tooltip("Número de frames para promediar la velocidad. Más = más suave pero más lento.")]
    [SerializeField, Range(2, 10)] private int velocitySamples = 5;

    [Header("─── Atracción ───")]
    [Tooltip("Velocidad en m/s a la que el objeto vuela hacia la mano.")]
    [SerializeField] private float attractionSpeed = 8f;

    [Header("─── VFX Opcionales ───")]
    [Tooltip("LineRenderer para el rayo visual (opcional).")]
    [SerializeField] private LineRenderer gloveBeam;

    [Tooltip("Partículas que se reproducen al lanzar la atracción (opcional).")]
    [SerializeField] private ParticleSystem attractParticles;

    [Header("─── Debug ───")]
    [SerializeField] private bool showGizmos = true;

    // ═══════════════════════════════════════════════
    //  ESTADO PRIVADO
    // ═══════════════════════════════════════════════

    private GravityGlovable currentTarget;   // Objeto al que apuntamos ahora
    private GravityGlovable lastTarget;      // Último objeto apuntado (persiste aunque dejemos de apuntar)

    private bool isButtonHeld;
    private float cooldownTimer;

    // Velocidad del mando (promediada)
    private Queue<Vector3> velocityQueue;
    private Vector3 previousPos;
    private Vector3 smoothVelocity;

    // ═══════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        velocityQueue = new Queue<Vector3>();
    }

    private void OnEnable()
    {
        if (activateAction == null) return;
        activateAction.action.Enable();
        activateAction.action.performed += OnButtonPressed;
        activateAction.action.canceled  += OnButtonReleased;
    }

    private void OnDisable()
    {
        if (activateAction == null) return;
        activateAction.action.performed -= OnButtonPressed;
        activateAction.action.canceled  -= OnButtonReleased;
    }

    private void Update()
    {
        UpdateSmoothedVelocity();

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (!isButtonHeld)
        {
            ClearCurrentTarget();
            SetBeamActive(false);
            return;
        }

        DoRaycast();

        // Intentar detectar el gesto si tenemos un target válido
        if (lastTarget != null && !lastTarget.IsBeingAttracted && cooldownTimer <= 0f)
            CheckFlickGesture();
    }

    // ═══════════════════════════════════════════════
    //  INPUT CALLBACKS
    // ═══════════════════════════════════════════════

    private void OnButtonPressed(InputAction.CallbackContext ctx)
    {
        isButtonHeld = true;
    }

    private void OnButtonReleased(InputAction.CallbackContext ctx)
    {
        isButtonHeld = false;
        ClearCurrentTarget();
        SetBeamActive(false);
    }

    // ═══════════════════════════════════════════════
    //  RAYCAST & TARGET
    // ═══════════════════════════════════════════════

    private void DoRaycast()
    {
        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, attractableLayer))
        {
            // Buscar GravityGlovable en el objeto golpeado o su padre
            var glovable = hit.collider.GetComponentInParent<GravityGlovable>();

            if (glovable != null && !glovable.IsBeingAttracted)
            {
                // Nuevo target
                if (currentTarget != glovable)
                {
                    ClearCurrentTarget();
                    currentTarget = glovable;
                    lastTarget    = glovable;

                    currentTarget.SetHighlight(true);
                    currentTarget.OnArrived += HandleObjectArrived;
                }

                UpdateBeam(hit.point);
                return;
            }
        }

        // No hay hit válido
        ClearCurrentTarget();
        SetBeamActive(false);
    }

    private void ClearCurrentTarget()
    {
        if (currentTarget == null) return;

        currentTarget.SetHighlight(false);
        currentTarget.OnArrived -= HandleObjectArrived;
        currentTarget = null;
        // Nota: lastTarget se mantiene para poder atraer aunque soltemos la mira
    }

    // ═══════════════════════════════════════════════
    //  DETECCIÓN DEL GESTO
    // ═══════════════════════════════════════════════

    private void UpdateSmoothedVelocity()
    {
        Vector3 frameVelocity = (transform.position - previousPos) / Time.deltaTime;
        previousPos = transform.position;

        velocityQueue.Enqueue(frameVelocity);
        if (velocityQueue.Count > velocitySamples)
            velocityQueue.Dequeue();

        // Promediar
        smoothVelocity = Vector3.zero;
        foreach (var v in velocityQueue)
            smoothVelocity += v;
        smoothVelocity /= velocityQueue.Count;
    }

    private void CheckFlickGesture()
    {
        // El "flick hacia atrás" = el mando se mueve en dirección contraria a su forward
        // (apuntas → tiras del brazo hacia ti)
        float pullStrength = Vector3.Dot(smoothVelocity, -transform.forward);

        if (pullStrength >= flickThreshold)
        {
            TriggerAttraction();
        }
    }

    // ═══════════════════════════════════════════════
    //  ATRACCIÓN
    // ═══════════════════════════════════════════════

    private void TriggerAttraction()
    {
        if (lastTarget == null || lastTarget.IsBeingAttracted) return;

        // Suscribirse a la llegada del objeto
        lastTarget.OnArrived -= HandleObjectArrived;
        lastTarget.OnArrived += HandleObjectArrived;

        lastTarget.AttractTo(handAnchor != null ? handAnchor : transform, attractionSpeed);

        // VFX
        if (attractParticles != null)
            attractParticles.Play();

        cooldownTimer = attractCooldown;

        // Limpiar target actual (ya está volando)
        if (currentTarget != null)
        {
            currentTarget.OnArrived -= HandleObjectArrived; // evitar doble suscripción
            currentTarget = null;
        }

        Debug.Log($"[GravityGlove] ¡Atrayendo {lastTarget.name}! Pull strength: {Vector3.Dot(smoothVelocity, -transform.forward):F2} m/s");
    }

    // ═══════════════════════════════════════════════
    //  CALLBACK: OBJETO LLEGÓ A LA MANO
    // ═══════════════════════════════════════════════

    private void HandleObjectArrived(GravityGlovable obj)
    {
        obj.OnArrived -= HandleObjectArrived;

        if (obj == lastTarget) lastTarget = null;

        // ── Auto-grab con XRI ──
        if (directInteractor != null)
        {
            var grabInteractable = obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grabInteractable != null)
            {
                // XRI 2.x / 3.x compatible
                directInteractor.StartManualInteraction(grabInteractable as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable);
                Debug.Log($"[GravityGlove] Auto-grab activado en {obj.name}");
            }
        }

        // VFX
        if (attractParticles != null)
            attractParticles.Stop();
    }

    // ═══════════════════════════════════════════════
    //  BEAM / VFX
    // ═══════════════════════════════════════════════

    private void UpdateBeam(Vector3 hitPoint)
    {
        if (gloveBeam == null) return;

        gloveBeam.enabled = true;
        gloveBeam.SetPosition(0, transform.position);
        gloveBeam.SetPosition(1, hitPoint);
    }

    private void SetBeamActive(bool active)
    {
        if (gloveBeam != null)
            gloveBeam.enabled = active;
    }

    // ═══════════════════════════════════════════════
    //  GIZMOS (editor)
    // ═══════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        // Rayo
        Gizmos.color = currentTarget != null ? Color.cyan : Color.white;
        Gizmos.DrawRay(transform.position, transform.forward * maxRayDistance);

        // Hand anchor
        if (handAnchor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(handAnchor.position, 0.05f);
        }

        // Velocidad del mando
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, smoothVelocity * 0.3f);
    }
}
