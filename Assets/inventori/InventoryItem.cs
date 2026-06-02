using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRInventory
{
 
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class InventoryItem : MonoBehaviour
    {


        [Header("─── Inventario ───")]
        [SerializeField] private string itemTag = "Weapon";

  
       

        public string        ItemTag   => itemTag;
        public bool          IsSlotted => currentSlot != null;
        public InventorySlot CurrentSlot => currentSlot;

      

        private XRGrabInteractable grabInteractable;
        private Rigidbody          rb;

        private InventorySlot currentSlot;       
        private InventorySlot nearestReadySlot;  

        private bool isBeingHeld;
        private bool savedKinematic;             

       

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
            
            if (isBeingHeld)
                UpdateNearestSlot();
        }

        

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

           
            if (nearestReadySlot == best) return;

            nearestReadySlot?.SetReadyHighlight(false);
            nearestReadySlot = best;
            nearestReadySlot?.SetReadyHighlight(true);
        }

       

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            isBeingHeld = true;

           
            if (currentSlot != null)
            {
                currentSlot.UnslotCurrentItem();
                currentSlot = null;

                
                rb.isKinematic = savedKinematic;

                Debug.Log($"[Inventory] {name} extraído del slot.");
            }
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            isBeingHeld = false;

           
            nearestReadySlot?.SetReadyHighlight(false);

            if (nearestReadySlot != null)
            {
               
                savedKinematic = rb.isKinematic;

               
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
               
                nearestReadySlot = null;
                Debug.Log($"[Inventory] {name} soltado sin slot cercano.");
            }
        }

       

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.12f,
                $"Item Tag: \"{itemTag}\""
            );
        }
#endif
    }
}
