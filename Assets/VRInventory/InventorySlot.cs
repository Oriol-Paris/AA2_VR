using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRInventory
{
    /// <summary>
    /// Punto de anclaje del inventario físico VR.
    ///
    /// SETUP EN EDITOR:
    ///  1. Crea un GameObject vacío hijo del XR Origin (o de cualquier objeto).
    ///  2. Posiciónalo donde quieras el slot (cadera, espalda, pecho...).
    ///  3. Añade este componente. Los Gizmos mostrarán el radio y el label.
    ///  4. Configura AcceptedTag para filtrar qué objetos encajan aquí.
    /// </summary>
    public class InventorySlot : MonoBehaviour
    {
        // ── Registro estático global ──────────────────
        // Todos los slots activos se registran aquí para que
        // InventoryItem los encuentre sin FindObjectsOfType cada frame.
        public static readonly List<InventorySlot> AllSlots = new List<InventorySlot>();

        // ═══════════════════════════════════════════════
        //  INSPECTOR
        // ═══════════════════════════════════════════════

        [Header("─── Configuración ───")]
        [Tooltip("Tag que deben tener los InventoryItem para poder anclarse. Vacío = acepta todos.")]
        [SerializeField] private string acceptedTag = "Weapon";

        [Tooltip("Radio de detección. Dentro de este radio el slot se ilumina y acepta el objeto al soltarlo.")]
        [SerializeField] private float snapRadius = 0.12f;

        [Header("─── Offset del objeto anclado ───")]
        [Tooltip("Desplazamiento de posición local cuando el objeto está anclado en este slot.")]
        [SerializeField] private Vector3 itemPositionOffset = Vector3.zero;

        [Tooltip("Desplazamiento de rotación local cuando el objeto está anclado en este slot.")]
        [SerializeField] private Vector3 itemRotationOffset = Vector3.zero;

        [Header("─── Visual en Play ───")]
        [Tooltip("Color del anillo en reposo.")]
        [SerializeField] private Color idleColor    = new Color(0.2f, 0.5f, 1.0f, 0.25f);
        [Tooltip("Color del anillo cuando hay un objeto compatible cerca (ready to snap).")]
        [SerializeField] private Color readyColor   = new Color(0.2f, 1.0f, 0.5f, 1.00f);
        [Tooltip("Color del anillo cuando el slot ya está ocupado.")]
        [SerializeField] private Color occupiedColor = new Color(0.4f, 0.4f, 0.4f, 0.12f);

        [Tooltip("Radio visual del anillo (independiente del radio de detección).")]
        [SerializeField] private float ringRadius = 0.07f;

        [Tooltip("Número de segmentos del anillo. Más = más suave.")]
        [SerializeField, Range(16, 64)] private int ringSegments = 48;

        [Header("─── Gizmos (Editor) ───")]
        [Tooltip("Nombre que aparece en el editor sobre el slot.")]
        [SerializeField] private string slotLabel = "Slot";
        [SerializeField] private Color  gizmoColor = new Color(0f, 0.8f, 1f, 1f);

        // ═══════════════════════════════════════════════
        //  PROPIEDADES PÚBLICAS
        // ═══════════════════════════════════════════════

        public bool          IsOccupied   => currentItem != null;
        public InventoryItem CurrentItem  => currentItem;
        public float         SnapRadius   => snapRadius;
        public string        AcceptedTag  => acceptedTag;

        // ═══════════════════════════════════════════════
        //  ESTADO PRIVADO
        // ═══════════════════════════════════════════════

        private InventoryItem currentItem;
        private LineRenderer  ringRenderer;
        private Material      ringMaterial;
        private Coroutine     visualCoroutine;

        private enum SlotState { Idle, Ready, Occupied }
        private SlotState currentState = SlotState.Idle;

        // ═══════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ═══════════════════════════════════════════════

        private void OnEnable()
        {
            AllSlots.Add(this);
        }

        private void OnDisable()
        {
            AllSlots.Remove(this);
        }

        private void Start()
        {
            BuildRingVisual();
            ApplyState(SlotState.Idle);
        }

        // ═══════════════════════════════════════════════
        //  API PÚBLICA
        // ═══════════════════════════════════════════════

        /// <summary>¿Puede este slot aceptar el item dado?</summary>
        public bool CanAccept(InventoryItem item)
        {
            if (IsOccupied) return false;
            if (string.IsNullOrEmpty(acceptedTag)) return true;
            return item.ItemTag == acceptedTag;
        }

        /// <summary>Ancora el item en este slot.</summary>
        public void SlotItem(InventoryItem item)
        {
            currentItem = item;
            item.transform.SetParent(transform);
            item.transform.localPosition = itemPositionOffset;
            item.transform.localRotation = Quaternion.Euler(itemRotationOffset);
            ApplyState(SlotState.Occupied);
        }

        /// <summary>Libera el item actualmente anclado.</summary>
        public void UnslotCurrentItem()
        {
            if (currentItem == null) return;
            currentItem.transform.SetParent(null);
            currentItem = null;
            ApplyState(SlotState.Idle);
        }

        /// <summary>
        /// Llamado por InventoryItem mientras sostiene un objeto compatible
        /// y lo acerca / aleja del radio.
        /// </summary>
        public void SetReadyHighlight(bool active)
        {
            if (IsOccupied) return;
            ApplyState(active ? SlotState.Ready : SlotState.Idle);
        }

        // ═══════════════════════════════════════════════
        //  LÓGICA DE VISUAL
        // ═══════════════════════════════════════════════

        private void ApplyState(SlotState newState)
        {
            if (currentState == newState && Application.isPlaying) return;
            currentState = newState;

            if (visualCoroutine != null) StopCoroutine(visualCoroutine);

            switch (newState)
            {
                case SlotState.Idle:
                    visualCoroutine = StartCoroutine(FadeTo(idleColor, 0.35f));
                    break;
                case SlotState.Ready:
                    visualCoroutine = StartCoroutine(PulseLoop(readyColor));
                    break;
                case SlotState.Occupied:
                    visualCoroutine = StartCoroutine(FadeTo(occupiedColor, 0.5f));
                    break;
            }
        }

        private IEnumerator FadeTo(Color target, float duration)
        {
            if (ringMaterial == null) yield break;

            Color start   = ringMaterial.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetRingColor(Color.Lerp(start, target, elapsed / duration));
                yield return null;
            }
            SetRingColor(target);
        }

        private IEnumerator PulseLoop(Color baseColor)
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * 4.5f;
                float pulse = (Mathf.Sin(t) + 1f) * 0.5f; // 0 → 1

                Color c = baseColor * (0.55f + pulse * 0.45f);
                c.a = baseColor.a * (0.55f + pulse * 0.45f);
                SetRingColor(c);

                // Escalar ligeramente el anillo para que "respire"
                if (ringRenderer != null)
                {
                    float scale = 1f + pulse * 0.08f;
                    ringRenderer.transform.localScale = Vector3.one * scale;
                }
                yield return null;
            }
        }

        private void SetRingColor(Color color)
        {
            if (ringMaterial != null)
                ringMaterial.color = color;
        }

        // ═══════════════════════════════════════════════
        //  CONSTRUCCIÓN DEL ANILLO EN RUNTIME
        // ═══════════════════════════════════════════════

        private void BuildRingVisual()
        {
            var ringObj = new GameObject("_SlotRing");
            ringObj.transform.SetParent(transform);
            ringObj.transform.localPosition = Vector3.zero;
            ringObj.transform.localRotation = Quaternion.identity;

            ringRenderer = ringObj.AddComponent<LineRenderer>();
            ringRenderer.loop            = true;
            ringRenderer.useWorldSpace   = false;
            ringRenderer.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows     = false;
            ringRenderer.startWidth         =
            ringRenderer.endWidth           = 0.004f;
            ringRenderer.positionCount      = ringSegments;

            // Puntos del círculo en el plano XZ
            for (int i = 0; i < ringSegments; i++)
            {
                float angle = (float)i / ringSegments * Mathf.PI * 2f;
                ringRenderer.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    0f,
                    Mathf.Sin(angle) * ringRadius
                ));
            }

            // Material: busca un shader compatible con URP o Built-in
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");

            ringMaterial = new Material(shader)
            {
                color = idleColor
            };

            // Habilitar transparencia si el shader lo soporta
            if (ringMaterial.HasProperty("_Surface"))
                ringMaterial.SetFloat("_Surface", 1f); // Transparent en URP

            ringRenderer.material = ringMaterial;
        }

        // ═══════════════════════════════════════════════
        //  GIZMOS  (solo en Editor)
        // ═══════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Esfera de detección semitransparente
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.06f);
            Gizmos.DrawSphere(transform.position, snapRadius);

            // Wireframe del radio de snap
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.55f);
            Gizmos.DrawWireSphere(transform.position, snapRadius);

            // Ejes de orientación del slot
            float axisLen = 0.035f;
            Gizmos.color = new Color(1f, 0.3f, 0.3f); Gizmos.DrawRay(transform.position, transform.right   * axisLen);
            Gizmos.color = new Color(0.3f, 1f, 0.3f); Gizmos.DrawRay(transform.position, transform.up      * axisLen);
            Gizmos.color = new Color(0.3f, 0.5f, 1f); Gizmos.DrawRay(transform.position, transform.forward * axisLen);
        }

        private void OnDrawGizmosSelected()
        {
            // Punto exacto donde quedará el objeto anclado
            Vector3 snapWorldPos = transform.TransformPoint(itemPositionOffset);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(snapWorldPos, 0.022f);
            Gizmos.DrawLine(transform.position, snapWorldPos);

            // Label con info del slot
            UnityEditor.Handles.color = gizmoColor;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (snapRadius + 0.03f),
                $"◈  {slotLabel}\nTag: \"{acceptedTag}\"\nR: {snapRadius:F2}m"
            );
        }
#endif
    }
}
