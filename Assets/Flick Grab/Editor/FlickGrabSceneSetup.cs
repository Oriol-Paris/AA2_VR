using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using FlickGrab;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;

public class FlickGrabSceneSetup : EditorWindow
{
    [MenuItem("Tools/Flick Grab/Setup Demo Scene")]
    public static void ShowWindow()
    {
        GetWindow<FlickGrabSceneSetup>("Flick Grab Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Flick Grab Scene Setup", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Setup Interactor on Right Hand"))
        {
            SetupInteractor();
        }

        if (GUILayout.Button("Make Selected Objects Flick-Grabbable"))
        {
            MakeSelectedGrabbable();
        }

        if (GUILayout.Button("Add Spawner to Scene"))
        {
            AddSpawner();
        }
        
        EditorGUILayout.HelpBox("1. Open the FlickGrabDemo scene.\n2. Click 'Setup Interactor' to add components to your XR Origin.\n3. Select any objects and click 'Make Flick-Grabbable'.\n4. Use 'Add Spawner' to spawn items by pressing 'A' in VR.", MessageType.Info);
    }

    private void AddSpawner()
    {
        GameObject spawnerObj = new GameObject("FlickGrabbableSpawner");
        spawnerObj.transform.position = Vector3.zero;
        FlickGrabbableSpawner spawner = spawnerObj.AddComponent<FlickGrabbableSpawner>();
        
        // Try to auto-link the spawner to the XR Origin for position
        var origin = Object.FindFirstObjectByType<XROrigin>();
        if (origin != null)
        {
            spawnerObj.transform.SetParent(origin.transform);
            spawnerObj.transform.localPosition = Vector3.zero;
        }

        Undo.RegisterCreatedObjectUndo(spawnerObj, "Add Flick Grabbable Spawner");
        Selection.activeGameObject = spawnerObj;
        
        Debug.Log("[FlickGrab] Added Spawner to scene. Don't forget to assign the 'Spawn Action' (e.g., PrimaryButton) in the Inspector!");
    }

    private void SetupInteractor()
    {
        XRRayInteractor ray = null;
        
        // 1. Try Selection
        if (Selection.activeGameObject != null)
        {
            ray = Selection.activeGameObject.GetComponentInChildren<XRRayInteractor>();
            if (ray == null) ray = Selection.activeGameObject.GetComponentInParent<XRRayInteractor>();
        }
        
        // 2. Fallback to finding in scene
        if (ray == null)
        {
            XRRayInteractor[] rays = Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
            foreach (var r in rays)
            {
                if (r.gameObject.name.ToLower().Contains("right"))
                {
                    ray = r;
                    break;
                }
            }
            if (ray == null && rays.Length > 0) ray = rays[0];
        }

        if (ray == null)
        {
            EditorUtility.DisplayDialog("Interactor Not Found", 
                "Could not find an XR Ray Interactor. Please select your Right Hand controller in the Hierarchy and try again.", "OK");
            return;
        }

        GameObject handObj = ray.gameObject;
        Undo.RegisterCompleteObjectUndo(handObj, "Setup Flick Grab Interactor");
        
        FlickGestureDetector detector = handObj.GetComponent<FlickGestureDetector>() ?? handObj.AddComponent<FlickGestureDetector>();
        FlickGrabInteractor flickInteractor = handObj.GetComponent<FlickGrabInteractor>() ?? handObj.AddComponent<FlickGrabInteractor>();

        SerializedObject so = new SerializedObject(flickInteractor);
        so.FindProperty("rayInteractor").objectReferenceValue = ray;
        so.FindProperty("gestureDetector").objectReferenceValue = detector;
        
        XRDirectInteractor direct = handObj.GetComponentInChildren<XRDirectInteractor>();
        if (direct) so.FindProperty("directInteractor").objectReferenceValue = direct;
        
        so.FindProperty("handAnchor").objectReferenceValue = handObj.transform;

        // Try to find default activate action
        if (so.FindProperty("activateAction").objectReferenceValue == null)
        {
            var actionAssets = AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset");
            if (actionAssets.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(actionAssets[0]);
                InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                var action = asset.FindAction("XRI RightHand/Interaction/Activate");
                if (action != null)
                {
                    // Note: InputActionReference is what the field expects
                    var references = AssetDatabase.FindAssets("t:InputActionReference");
                    foreach (var guid in references)
                    {
                        var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(AssetDatabase.GUIDToAssetPath(guid));
                        if (reference.action.id == action.id)
                        {
                            so.FindProperty("activateAction").objectReferenceValue = reference;
                            break;
                        }
                    }
                }
            }
        }

        LineRenderer line = handObj.GetComponent<LineRenderer>();
        if (!line)
        {
            line = handObj.AddComponent<LineRenderer>();
            line.startWidth = 0.01f;
            line.endWidth = 0.01f;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (shader != null) line.material = new Material(shader);
            line.startColor = Color.cyan;
            line.endColor = Color.blue;
        }
        so.FindProperty("beamLine").objectReferenceValue = line;

        so.ApplyModifiedProperties();
        
        EditorUtility.SetDirty(flickInteractor);
        EditorUtility.SetDirty(handObj);
        
        Debug.Log($"[FlickGrab] Successfully setup FlickGrabInteractor on {handObj.name}", handObj);
    }

    private void MakeSelectedGrabbable()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            Undo.RecordObject(obj, "Make Flick-Grabbable");
            if (!obj.GetComponent<Rigidbody>()) obj.AddComponent<Rigidbody>();
            if (!obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>()) 
                obj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            
            if (!obj.GetComponent<FlickGrabbable>())
                obj.AddComponent<FlickGrabbable>();
            
            EditorUtility.SetDirty(obj);
            Debug.Log($"Made {obj.name} Flick-Grabbable");
        }
    }
}
