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
        [MenuItem("Tools/Flick Grab/Setup Interactor (Both Hands)")]
        public static void SetupInteractor()
        {
            XRRayInteractor[] rays = Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
            
            if (rays.Length == 0)
            {
                EditorUtility.DisplayDialog("Interactors Not Found", 
                    "Could not find any XR Ray Interactors in the scene. Please ensure your XR Rig is set up correctly.", "OK");
                return;
            }

            int count = 0;
            foreach (var ray in rays)
            {
                string handName = ray.gameObject.name.ToLower();
                bool isRight = handName.Contains("right");
                bool isLeft = handName.Contains("left");
                
                // If we can't tell by name, we might just set it up anyway if it's explicitly selected or if there's only one
                if (!isRight && !isLeft && rays.Length == 1) isRight = true; // Default to right if only one

                SetupHand(ray, isRight);
                count++;
            }
            
            Debug.Log($"[FlickGrab] Successfully setup {count} FlickGrabInteractors.");
        }

        private static void SetupHand(XRRayInteractor ray, bool isRight)
        {
            GameObject handObj = ray.gameObject;
            Undo.RegisterCompleteObjectUndo(handObj, "Setup Flick Grab");
            
            FlickGestureDetector detector = handObj.GetComponent<FlickGestureDetector>() ?? handObj.AddComponent<FlickGestureDetector>();
            FlickGrabInteractor flickInteractor = handObj.GetComponent<FlickGrabInteractor>() ?? handObj.AddComponent<FlickGrabInteractor>();

            SerializedObject so = new SerializedObject(flickInteractor);
            so.FindProperty("rayInteractor").objectReferenceValue = ray;
            so.FindProperty("gestureDetector").objectReferenceValue = detector;
            so.FindProperty("directInteractor").objectReferenceValue = handObj.GetComponentInChildren<XRDirectInteractor>();
            so.FindProperty("handAnchor").objectReferenceValue = handObj.transform;

            // Attempt to auto-assign Actions from XRI Default Input Actions
            string side = isRight ? "RightHand" : "LeftHand";
            
            // Setup Activate
            string[] activateGuids = AssetDatabase.FindAssets($"XRI {side} Activate t:InputActionReference");
            if (activateGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(activateGuids[0]);
                InputActionReference actionRef = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
                so.FindProperty("activateAction").objectReferenceValue = actionRef;
                Debug.Log($"[FlickGrab] Assigned Activate Action: {path}");
            }
            else
            {
                Debug.LogWarning($"[FlickGrab] Could not find Activate Action for {side}");
            }

            // Setup Thumbstick
            string[] thumbstickGuids = AssetDatabase.FindAssets($"XRI {side} Move t:InputActionReference");
            if (thumbstickGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(thumbstickGuids[0]);
                InputActionReference actionRef = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
                so.FindProperty("thumbstickAction").objectReferenceValue = actionRef;
                Debug.Log($"[FlickGrab] Assigned Thumbstick Action: {path}");
            }
            else
            {
                Debug.LogWarning($"[FlickGrab] Could not find Thumbstick Action for {side}");
            }

            // Setup LineRenderer for the beam
            LineRenderer line = handObj.GetComponent<LineRenderer>();
            if (!line)
            {
                line = handObj.AddComponent<LineRenderer>();
                line.startWidth = 0.01f;
                line.endWidth = 0.01f;
                
                // Try URP shader first, then fallback
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") 
                                ?? Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Sprites/Default") 
                                ?? Shader.Find("Legacy Shaders/Transparent/Diffuse");
                                
                if (shader != null) line.material = new Material(shader);
                line.startColor = Color.red;
                line.endColor = new Color(1, 0, 0, 0);
            }
            so.FindProperty("beamLine").objectReferenceValue = line;

            so.ApplyModifiedProperties();
            
            // Force save and mark dirty to ensure it persists in the inspector
            EditorUtility.SetDirty(flickInteractor);
            EditorUtility.SetDirty(handObj);
            
            if (line) EditorUtility.SetDirty(line);
            if (detector) EditorUtility.SetDirty(detector);
            
            Debug.Log($"[FlickGrab] Setup FlickGrabInteractor on {handObj.name} (Side: {(isRight ? "Right" : "Left")})", handObj);
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

    }
}
