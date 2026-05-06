using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using FlickGrab;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

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
        
        EditorGUILayout.HelpBox("1. Open the FlickGrabDemo scene.\n2. Click 'Setup Interactor' to add components to your XR Origin.\n3. Select any cubes/spheres and click 'Make Flick-Grabbable'.", MessageType.Info);
    }

    private void SetupInteractor()
    {
        XRRayInteractor ray = Selection.activeGameObject?.GetComponentInChildren<XRRayInteractor>();
        if (ray == null)
        {
            ray = Object.FindFirstObjectByType<XRRayInteractor>();
        }

        if (ray == null)
        {
            Debug.LogError("Could not find an XR Ray Interactor in the scene. Please select the Right Hand controller or an XR Origin.");
            return;
        }

        GameObject handObj = ray.gameObject;
        
        // Add GestureDetector
        if (!handObj.GetComponent<FlickGestureDetector>())
            handObj.AddComponent<FlickGestureDetector>();

        // Add FlickGrabInteractor
        FlickGrabInteractor flickInteractor = handObj.GetComponent<FlickGrabInteractor>();
        if (!flickInteractor)
            flickInteractor = handObj.AddComponent<FlickGrabInteractor>();

        // Link references via Reflection or SerializedObject since they are private
        SerializedObject so = new SerializedObject(flickInteractor);
        so.FindProperty("rayInteractor").objectReferenceValue = ray;
        so.FindProperty("gestureDetector").objectReferenceValue = handObj.GetComponent<FlickGestureDetector>();
        
        // Try to find a direct interactor on same object
        XRDirectInteractor direct = handObj.GetComponent<XRDirectInteractor>();
        if (direct) so.FindProperty("directInteractor").objectReferenceValue = direct;
        
        // Set hand anchor
        so.FindProperty("handAnchor").objectReferenceValue = handObj.transform;

        // Add a line renderer for the beam if missing
        LineRenderer line = handObj.GetComponent<LineRenderer>();
        if (!line)
        {
            line = handObj.AddComponent<LineRenderer>();
            line.startWidth = 0.01f;
            line.endWidth = 0.01f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = Color.cyan;
            line.endColor = Color.blue;
        }
        so.FindProperty("beamLine").objectReferenceValue = line;

        so.ApplyModifiedProperties();
        
        Debug.Log($"Successfully setup FlickGrabInteractor on {handObj.name}");
    }

    private void MakeSelectedGrabbable()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            if (!obj.GetComponent<Rigidbody>()) obj.AddComponent<Rigidbody>();
            if (!obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>()) 
                obj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            
            if (!obj.GetComponent<FlickGrabbable>())
                obj.AddComponent<FlickGrabbable>();
            
            Debug.Log($"Made {obj.name} Flick-Grabbable");
        }
    }
}
