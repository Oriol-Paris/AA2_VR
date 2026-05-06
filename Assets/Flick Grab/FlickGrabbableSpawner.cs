using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FlickGrab;

public class FlickGrabbableSpawner : MonoBehaviour
{
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, 1, 2);
    [SerializeField] private InputActionReference spawnAction;
    
    private void OnEnable()
    {
        if (spawnAction != null)
        {
            spawnAction.action.Enable();
            spawnAction.action.performed += OnSpawnPerformed;
        }
    }

    private void OnDisable()
    {
        if (spawnAction != null)
        {
            spawnAction.action.performed -= OnSpawnPerformed;
        }
    }

    private void OnSpawnPerformed(InputAction.CallbackContext context)
    {
        SpawnCube();
    }
    
    public void SpawnCube()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "FlickGrabbableCube";
        cube.transform.position = transform.position + transform.forward * spawnOffset.z + transform.up * spawnOffset.y;
        cube.transform.localScale = Vector3.one * 0.2f;

        // Add Physics
        Rigidbody rb = cube.AddComponent<Rigidbody>();
        rb.mass = 1f;

        // Add XRI Interactable
        XRGrabInteractable grab = cube.AddComponent<XRGrabInteractable>();
        
        // Add Flick Grab Component
        cube.AddComponent<FlickGrabbable>();

        // Set material to something that supports emission
        Renderer rend = cube.GetComponent<Renderer>();
        // Use a more robust way to find a shader that likely exists in the project
        Shader standardShader = Shader.Find("Standard");
        if (standardShader == null) standardShader = Shader.Find("Universal Render Pipeline/Lit");
        
        if (standardShader != null)
        {
            rend.material = new Material(standardShader);
            rend.material.EnableKeyword("_EMISSION");
        }
        
        Debug.Log("Spawned a Flick-Grabbable Cube at " + cube.transform.position);
    }

    [ContextMenu("Spawn Now")]
    private void SpawnFromMenu()
    {
        SpawnCube();
    }
}
