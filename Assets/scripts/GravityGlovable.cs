using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Añade este componente a cualquier objeto que quieras que sea atraíble con el Gravity Glove.
/// Requiere un Rigidbody en el mismo GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GravityGlovable : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────

    [Header("Highlight Visual")]
    [Tooltip("Color del brillo cuando el objeto está apuntado.")]
    [SerializeField] private Color highlightColor = new Color(0.3f, 0.85f, 1f);

    [Tooltip("Intensidad máxima de la emisión.")]
    [SerializeField] private float highlightIntensity = 2.5f;

    [Tooltip("Velocidad del pulso del brillo.")]
    [SerializeField] private float highlightPulseSpeed = 3f;

    [Header("Attraction")]
    [Tooltip("Curva de velocidad del objeto mientras vuela hacia la mano. EaseIn = arranca lento y acelera.")]
    [SerializeField] private AnimationCurve attractionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Velocidad de rotación del objeto mientras vuela (grados/seg). 0 = sin rotación.")]
    [SerializeField] private float spinSpeed = 280f;

    // ─────────────────────────────────────────────
    //  Eventos públicos
    // ─────────────────────────────────────────────

    /// <summary>Se invoca cuando el objeto llega a la mano.</summary>
    public event Action<GravityGlovable> OnArrived;

    // ─────────────────────────────────────────────
    //  Estado público (solo lectura)
    // ─────────────────────────────────────────────

    public bool IsBeingAttracted { get; private set; }
    public bool IsHighlighted    { get; private set; }

    // ─────────────────────────────────────────────
    //  Privado
    // ─────────────────────────────────────────────

    private Rigidbody          rb;
    private Renderer[]         renderers;
    private MaterialPropertyBlock propBlock;
    private Collider[]         ownColliders;

    private Coroutine attractCoroutine;
    private Coroutine pulseCoroutine;

    // Estado de física guardado antes de la atracción
    private bool savedKinematic;
    private bool savedGravity;
    private Vector3 savedVelocity;

    // ─────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        rb           = GetComponent<Rigidbody>();
        renderers    = GetComponentsInChildren<Renderer>();
        propBlock    = new MaterialPropertyBlock();
        ownColliders = GetComponentsInChildren<Collider>();
    }

    // ─────────────────────────────────────────────
    //  API pública
    // ─────────────────────────────────────────────

    /// <summary>Activa o desactiva el efecto de brillo pulsante.</summary>
    public void SetHighlight(bool active)
    {
        if (IsHighlighted == active) return;
        IsHighlighted = active;

        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);

        if (active)
            pulseCoroutine = StartCoroutine(PulseRoutine());
        else
            SetEmission(Color.black); // apagar emisión
    }

    /// <summary>
    /// Lanza la atracción del objeto hacia <paramref name="target"/>.
    /// </summary>
    /// <param name="target">Transform de la mano destino.</param>
    /// <param name="speed">Velocidad en unidades/segundo.</param>
    public void AttractTo(Transform target, float speed)
    {
        if (IsBeingAttracted) return;

        SetHighlight(false);

        if (attractCoroutine != null)
            StopCoroutine(attractCoroutine);

        attractCoroutine = StartCoroutine(AttractionRoutine(target, speed));
    }

    // ─────────────────────────────────────────────
    //  Coroutines privadas
    // ─────────────────────────────────────────────

    private IEnumerator PulseRoutine()
    {
        float time = 0f;
        while (true)
        {
            time += Time.deltaTime * highlightPulseSpeed;
            // Seno → rango [0,1] → brillo pulsante
            float pulse = (Mathf.Sin(time) + 1f) * 0.5f;
            Color emissionColor = highlightColor * (highlightIntensity * (0.4f + pulse * 0.6f));
            SetEmission(emissionColor);
            yield return null;
        }
    }

    private IEnumerator AttractionRoutine(Transform target, float speed)
    {
        IsBeingAttracted = true;

        // ── Guardar y congelar física ──
        savedKinematic = rb.isKinematic;
        savedGravity   = rb.useGravity;
        savedVelocity  = rb.linearVelocity;
        rb.isKinematic = true;
        rb.useGravity  = false;

        // ── Desactivar colliders para no chocar en el trayecto ──
        foreach (var col in ownColliders)
            col.enabled = false;

        // ── Vuelo ──
        Vector3 startPos  = transform.position;
        float   distance  = Vector3.Distance(startPos, target.position);
        float   duration  = Mathf.Max(distance / speed, 0.05f);
        float   elapsed   = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t        = Mathf.Clamp01(elapsed / duration);
            float curved   = attractionCurve.Evaluate(t);

            // Lerp de posición siguiendo la curva
            // Recalcular hacia la mano en cada frame (la mano puede moverse)
            transform.position = Vector3.Lerp(startPos, target.position, curved);

            // Spin durante el vuelo
            if (spinSpeed > 0f)
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            yield return null;
        }

        // ── Asegurar posición final ──
        transform.position = target.position;

        // ── Reactivar colliders ──
        foreach (var col in ownColliders)
            col.enabled = true;

        // ── Restaurar física ──
        rb.isKinematic = savedKinematic;
        rb.useGravity  = savedGravity;
        rb.linearVelocity = Vector3.zero;

        IsBeingAttracted = false;

        // ── Notificar llegada ──
        OnArrived?.Invoke(this);
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    private void SetEmission(Color color)
    {
        foreach (var rend in renderers)
        {
            rend.GetPropertyBlock(propBlock);
            propBlock.SetColor("_EmissionColor", color);
            rend.SetPropertyBlock(propBlock);
        }
    }
}
