using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRInventory
{
    /// <summary>
    /// Añade este componente a cualquier objeto XRGrabInteractable que quieras
    /// que se pueda anclar en un InventorySlot.
    ///
    /// SETUP:
    ///  1. El objeto debe tener Rigidbody + XRGrabInteractable.
    ///  2. Configura ItemTag para que coincida con el AcceptedTag del slot deseado.
    ///  3. Al soltar el objeto dentro del radio de un slot compatible → snap automático.
    ///  4. Al volver a agarrarlo → se libera del slot.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class InventoryItem : MonoBehaviour
    {
        // ═══════════════════════════════════════════════
        //  INSPECTOR
        // ═══════════════════════════════════════════════

        [Header("─── Inventario ───")]
        [Tooltip("Debe coincidir con el AcceptedTag del slot en el que quieras que encaje.")]
        [SerializeField] private string itemTag = "Weapon";

        // ═══════════════════════════════════════════════
        //  PROPIEDADES PÚBLICAS
        // ═══════════════════════════════════════════════

        public string        ItemTag   => itemTag;
        public bool          IsSlotted => currentSlot != null;
        public InventorySlot CurrentSlot => currentSlot;

        // ═══════════════════════════════════════════════
        //  ESTADO PRIVADO
        // ═══════════════════════════════════════════════

        private XRGrabInteractable grabInteractable;
        private Rigidbody          rb;

        private InventorySlot currentSlot;       // Slot donde está anclado actualmente
        private InventorySlot nearestReadySlot;  // Slot más cercano mientras se sostiene

        private bool isBeingHeld;
        private bool savedKinematic;             // Estado de física guardado antes del snap

        // ═══════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ═══════════════════════════════════════════════

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            rb               = GetComponent<Rigidbody>();

            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        private void OnDestroy()
        {
            if (grabInteractable == null) return;
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }

        private void Update()
        {
            // Solo comprobar slots mientras el objeto está en la mano
            if (isBeingHeld)
                UpdateNearestSlot();
        }

        // ═══════════════════════════════════════════════
        //  DETECCIÓN DE SLOTS CERCANOS (cada frame mientras se sostiene)
        // ═══════════════════════════════════════════════

        private void UpdateNearestSlot()
        {
            InventorySlot best     = null;
            float         bestDist = float.MaxValue;

            foreach (var slot in InventorySlot.AllSlots)
            {
                if (!slot.CanAccept(this)) continue;

                float dist = Vector3.Distance(transform.position, slot.transform.position);
                if (dist < slot.SnapRadius && dist < bestDist)
                {
                    best     = slot;
                    bestDist = dist;
                }
            }

            // Actualizar highlight solo si el slot candidato cambió
            if (nearestReadySlot == best) return;

            nearestReadySlot?.SetReadyHighlight(false);
            nearestReadySlot = best;
            nearestReadySlot?.SetReadyHighlight(true);
        }

        // ═══════════════════════════════════════════════
        //  EVENTOS DE GRAB / RELEASE
        // ═══════════════════════════════════════════════

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            isBeingHeld = true;

            // Si el objeto estaba en un slot, liberarlo
            if (currentSlot != null)
            {
                currentSlot.UnslotCurrentItem();
                currentSlot = null;

                // Restaurar estado de física original
                rb.isKinematic = savedKinematic;

                Debug.Log($"[Inventory] {name} extraído del slot.");
            }
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            isBeingHeld = false;

            // Limpiar el highlight del slot señalado
            nearestReadySlot?.SetReadyHighlight(false);

            if (nearestReadySlot != null)
            {
                // ── SNAP AL SLOT ──────────────────────────
                savedKinematic = rb.isKinematic;

                // Congelar física para que no se caiga ni interfiera con el parenting
                rb.isKinematic    = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                currentSlot       = nearestReadySlot;
                nearestReadySlot  = null;

                currentSlot.SlotItem(this);
                Debug.Log($"[Inventory] {name} anclado en '{currentSlot.name}'.");
            }
            else
            {
                // Drop normal: no había slot cercano compatible
                nearestReadySlot = null;
                Debug.Log($"[Inventory] {name} soltado sin slot cercano.");
            }
        }

        // ═══════════════════════════════════════════════
        //  GIZMOS (Editor)
        // ═══════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.12f,
                $"🎒 Item Tag: \"{itemTag}\""
            );
        }
#endif
    }
}
