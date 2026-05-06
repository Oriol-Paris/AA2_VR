// ⚠️  ESTE ARCHIVO DEBE ESTAR EN UNA CARPETA "Editor"
//     Assets/TuCarpeta/Editor/InventorySetupWindow.cs
//
//     Si no está en Editor/, causará errores de compilación en builds.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.XR.CoreUtils;
using VRInventory;

public class InventorySetupWindow : EditorWindow
{
    // ── Parámetros del slot a crear ──
    private string  newSlotLabel   = "HipSlot_R";
    private string  newSlotTag     = "Weapon";
    private float   newSnapRadius  = 0.12f;
    private Vector3 newSlotOffset  = Vector3.zero;

    // ── Parámetros para hacer objetos inventariables ──
    private string  newItemTag     = "Weapon";

    [MenuItem("Tools/VR Inventory/Setup Window")]
    public static void ShowWindow()
    {
        GetWindow<InventorySetupWindow>("VR Inventory Setup");
    }

    private void OnGUI()
    {
        // ── Encabezado ──────────────────────────────
        GUILayout.Space(6);
        EditorGUILayout.LabelField("◈  VR Inventory Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Selecciona el GameObject donde quieres colocar el slot (ej: un hijo del XR Origin).\n" +
            "2. Configura los parámetros y pulsa 'Crear Slot'.\n" +
            "3. Para hacer objetos inventariables, selecciónalos y pulsa 'Hacer Inventariable'.",
            MessageType.Info);
        GUILayout.Space(8);

        // ── Sección: Crear Slot ──────────────────────
        EditorGUILayout.LabelField("─── Crear Slot de Inventario ───", EditorStyles.boldLabel);
        newSlotLabel  = EditorGUILayout.TextField("Nombre del slot",    newSlotLabel);
        newSlotTag    = EditorGUILayout.TextField("Tag aceptada",       newSlotTag);
        newSnapRadius = EditorGUILayout.FloatField("Radio de snap (m)", newSnapRadius);
        newSlotOffset = EditorGUILayout.Vector3Field("Offset del item anclado", newSlotOffset);

        GUILayout.Space(4);

        if (GUILayout.Button("➕  Crear Slot en el objeto seleccionado", GUILayout.Height(30)))
            CreateSlotOnSelected();

        if (GUILayout.Button("➕  Crear Slot en el XR Origin (auto-buscar)", GUILayout.Height(30)))
            CreateSlotOnXROrigin();

        GUILayout.Space(12);

        // ── Sección: Hacer objetos inventariables ──
        EditorGUILayout.LabelField("─── Hacer Objetos Inventariables ───", EditorStyles.boldLabel);
        newItemTag = EditorGUILayout.TextField("ItemTag a asignar", newItemTag);

        GUILayout.Space(4);

        if (GUILayout.Button("🎒  Hacer Inventariable(s) la selección", GUILayout.Height(30)))
            MakeSelectedInventoriable();

        GUILayout.Space(12);

        // ── Sección: Utilidades ─────────────────────
        EditorGUILayout.LabelField("─── Utilidades ───", EditorStyles.boldLabel);

        if (GUILayout.Button("📋  Listar todos los slots en escena"))
            ListAllSlots();
    }

    // ═══════════════════════════════════════════════
    //  CREAR SLOT EN OBJETO SELECCIONADO
    // ═══════════════════════════════════════════════

    private void CreateSlotOnSelected()
    {
        GameObject parent = Selection.activeGameObject;

        if (parent == null)
        {
            EditorUtility.DisplayDialog("Sin selección",
                "Selecciona el GameObject donde quieres crear el slot en el Hierarchy.", "OK");
            return;
        }

        CreateSlot(parent.transform);
    }

    // ═══════════════════════════════════════════════
    //  CREAR SLOT EN XR ORIGIN
    // ═══════════════════════════════════════════════

    private void CreateSlotOnXROrigin()
    {
        XROrigin origin = FindFirstObjectByType<XROrigin>();

        if (origin == null)
        {
            EditorUtility.DisplayDialog("XR Origin no encontrado",
                "No se encontró ningún XR Origin en la escena. " +
                "Selecciona manualmente el objeto padre e usa 'Crear Slot en el objeto seleccionado'.", "OK");
            return;
        }

        CreateSlot(origin.transform);
    }

    // ═══════════════════════════════════════════════
    //  LÓGICA COMÚN DE CREACIÓN
    // ═══════════════════════════════════════════════

    private void CreateSlot(Transform parent)
    {
        var slotObj = new GameObject(newSlotLabel);
        Undo.RegisterCreatedObjectUndo(slotObj, "Create InventorySlot");

        slotObj.transform.SetParent(parent);
        slotObj.transform.localPosition = Vector3.zero;
        slotObj.transform.localRotation = Quaternion.identity;

        InventorySlot slot = slotObj.AddComponent<InventorySlot>();

        // Asignar propiedades via SerializedObject para que sean Undo-able
        SerializedObject so = new SerializedObject(slot);
        so.FindProperty("acceptedTag").stringValue         = newSlotTag;
        so.FindProperty("snapRadius").floatValue           = newSnapRadius;
        so.FindProperty("itemPositionOffset").vector3Value = newSlotOffset;
        so.FindProperty("slotLabel").stringValue           = newSlotLabel;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = slotObj;
        EditorGUIUtility.PingObject(slotObj);

        Debug.Log($"[VRInventory] Slot '{newSlotLabel}' creado como hijo de '{parent.name}'. " +
                  $"Ajusta su posición en la escena.", slotObj);
    }

    // ═══════════════════════════════════════════════
    //  HACER OBJETOS INVENTARIABLES
    // ═══════════════════════════════════════════════

    private void MakeSelectedInventoriable()
    {
        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Sin selección",
                "Selecciona uno o más GameObjects en el Hierarchy.", "OK");
            return;
        }

        int count = 0;
        foreach (GameObject obj in Selection.gameObjects)
        {
            Undo.RecordObject(obj, "Make Inventoriable");

            // Rigidbody
            if (!obj.GetComponent<Rigidbody>())
                Undo.AddComponent<Rigidbody>(obj);

            // XRGrabInteractable
            if (!obj.GetComponent<XRGrabInteractable>())
                Undo.AddComponent<XRGrabInteractable>(obj);

            // InventoryItem
            InventoryItem item = obj.GetComponent<InventoryItem>();
            if (!item)
                item = Undo.AddComponent<InventoryItem>(obj);

            // Asignar tag
            SerializedObject so = new SerializedObject(item);
            so.FindProperty("itemTag").stringValue = newItemTag;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(obj);
            count++;
        }

        Debug.Log($"[VRInventory] {count} objeto(s) configurados como InventoryItem con tag '{newItemTag}'.");
    }

    // ═══════════════════════════════════════════════
    //  LISTAR SLOTS
    // ═══════════════════════════════════════════════

    private void ListAllSlots()
    {
        InventorySlot[] slots = FindObjectsByType<InventorySlot>(FindObjectsSortMode.None);

        if (slots.Length == 0)
        {
            Debug.Log("[VRInventory] No hay ningún InventorySlot en la escena.");
            return;
        }

        Debug.Log($"[VRInventory] {slots.Length} slot(s) en escena:");
        foreach (var s in slots)
        {
            Debug.Log($"  • {s.name}  |  Tag: '{s.AcceptedTag}'  |  Radio: {s.SnapRadius}m", s.gameObject);
        }
    }
}
#endif
