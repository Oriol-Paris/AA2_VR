using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FlickGrab
{
    public class FlickGrabAutoSpawner : MonoBehaviour
    {
        [SerializeField] private float spawnDistance = 2.0f;
        [SerializeField] private Vector3 cubeScale = new Vector3(0.2f, 0.2f, 0.2f);
        [SerializeField] private Color cubeColor = Color.white;

        void Start()
        {
            SpawnCube();
        }

        [ContextMenu("Spawn Now")]
        public void SpawnCube()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "FlickGrabbableCube";
            
            // Position it in front of the spawner or camera
            Transform spawnRoot = Camera.main != null ? Camera.main.transform : transform;
            cube.transform.position = spawnRoot.position + (spawnRoot.forward * spawnDistance);
            cube.transform.localScale = cubeScale;

            // Add Physics
            Rigidbody rb = cube.GetComponent<Rigidbody>();
            if (rb == null) rb = cube.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Add XRI
            if (!cube.GetComponent<XRGrabInteractable>())
            {
                var grab = cube.AddComponent<XRGrabInteractable>();
                // Make it easy to grab
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            }

            // Add Flick Grab logic
            if (!cube.GetComponent<FlickGrabbable>())
            {
                cube.AddComponent<FlickGrabbable>();
            }

            // Visuals
            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Create a material that supports emission for the highlight
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                
                Material mat = new Material(shader);
                mat.color = cubeColor;
                mat.EnableKeyword("_EMISSION");
                renderer.material = mat;
            }

            Debug.Log($"[FlickGrab] Spawned auto-configured cube at {cube.transform.position}");
        }
    }
}
