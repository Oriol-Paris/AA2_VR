using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using FlickGrab;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

namespace FlickGrab.Editor
{
    public class FlickGrabSceneSetup : EditorWindow
    {
        [MenuItem("Tools/Flick Grab/Setup Interactor")]
        public static void SetupInteractor()
        {
            XRRayInteractor ray = null;
            
            // Try Selection
            if (Selection.activeGameObject != null)
            {
                ray = Selection.activeGameObject.GetComponentInChildren<XRRayInteractor>();
                if (ray == null) ray = Selection.activeGameObject.GetComponentInParent<XRRayInteractor>();
            }
            
            // Fallback to finding in scene (prioritize Right hand)
            if (ray == null)
            {
                XRRayInteractor[] rays = Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
                foreach (var r in rays)
                {
                    if (r.gameObject.name.ToLower().Contains("right")) { ray = r; break; }
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
            Undo.RegisterCompleteObjectUndo(handObj, "Setup Flick Grab");
            
            FlickGestureDetector detector = handObj.GetComponent<FlickGestureDetector>() ?? handObj.AddComponent<FlickGestureDetector>();
            FlickGrabInteractor flickInteractor = handObj.GetComponent<FlickGrabInteractor>() ?? handObj.AddComponent<FlickGrabInteractor>();

            SerializedObject so = new SerializedObject(flickInteractor);
            so.FindProperty("rayInteractor").objectReferenceValue = ray;
            so.FindProperty("gestureDetector").objectReferenceValue = detector;
            so.FindProperty("directInteractor").objectReferenceValue = handObj.GetComponentInChildren<XRDirectInteractor>();
            so.FindProperty("handAnchor").objectReferenceValue = handObj.transform;

            // Setup LineRenderer for the beam
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
            
            Debug.Log($"[FlickGrab] Successfully setup FlickGrabInteractor on {handObj.name}. Ensure you assign the 'Activate Action' in the inspector!", handObj);
        }

        [MenuItem("Tools/Flick Grab/Make Selected Flick-Grabbable")]
        public static void MakeSelectedGrabbable()
        {
            if (Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select one or more GameObjects in the Hierarchy.", "OK");
                return;
            }

            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RegisterCompleteObjectUndo(obj, "Make Flick-Grabbable");
                
                if (!obj.GetComponent<Rigidbody>()) obj.AddComponent<Rigidbody>();
                if (!obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>()) 
                    obj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                
                if (!obj.GetComponent<FlickGrabbable>())
                    obj.AddComponent<FlickGrabbable>();
                
                Debug.Log($"[FlickGrab] Made {obj.name} Flick-Grabbable");
            }
        }

        [MenuItem("Tools/Flick Grab/Add Auto-Spawner")]
        public static void AddAutoSpawner()
        {
            GameObject spawnerObj = new GameObject("FlickGrabAutoSpawner");
            spawnerObj.AddComponent<FlickGrabAutoSpawner>();
            Undo.RegisterCreatedObjectUndo(spawnerObj, "Add Auto Spawner");
            Selection.activeGameObject = spawnerObj;
            Debug.Log("[FlickGrab] Added Auto-Spawner to scene. It will spawn a cube in front of the camera on Start.");
        }

        [MenuItem("Tools/Flick Grab/Add Debug Overlay")]
        public static void AddDebugOverlay()
        {
            GameObject canvasObj = new GameObject("FlickGrabDebugCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            RectTransform rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(4, 3);
            
            // Position in front of camera
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null)
            {
                canvasObj.transform.position = cam.position + cam.forward * 2f + cam.right * 1f;
                canvasObj.transform.rotation = Quaternion.LookRotation(canvasObj.transform.position - cam.position);
            }
            else
            {
                canvasObj.transform.position = new Vector3(0.5f, 1.5f, 1.5f);
            }
            canvasObj.transform.localScale = Vector3.one * 0.5f;

            GameObject textObj = new GameObject("LogText");
            textObj.transform.SetParent(canvasObj.transform, false);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 0.15f;
            text.alignment = TextAlignmentOptions.BottomLeft;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            FlickGrabDebugger debugger = canvasObj.AddComponent<FlickGrabDebugger>();
            SerializedObject so = new SerializedObject(debugger);
            so.FindProperty("logText").objectReferenceValue = text;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(canvasObj, "Add Debug Overlay");
            Debug.Log("[FlickGrab] Added Debug Overlay to scene.");
        }
    }
}
