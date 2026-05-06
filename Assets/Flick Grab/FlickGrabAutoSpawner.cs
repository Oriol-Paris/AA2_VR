using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FlickGrab
{
    public class FlickGrabAutoSpawner : MonoBehaviour
    {
        [SerializeField] private float spawnDistance = 5.0f;
        [SerializeField] private Vector3 cubeScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color cubeColor = Color.white;

        void Start()
        {
            EnsureInteractorExists();
            SpawnCube();
        }

        private void EnsureInteractorExists()
        {
            // Search for both active and inactive objects
            FlickGrabInteractor[] interactors = Object.FindObjectsByType<FlickGrabInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            bool anyOnCamera = false;
            foreach (var inter in interactors)
            {
                if (Camera.main != null && inter.gameObject == Camera.main.gameObject)
                {
                    anyOnCamera = true;
                }
            }

            // In Editor, we ALWAYS want one on the Camera if we are simulating
            if (Application.isEditor && !anyOnCamera)
            {
                SetupInteractorOnCamera();
            }
            else if (interactors.Length == 0)
            {
                SetupInteractorOnCamera();
            }
        }

        private void SetupInteractorOnCamera()
        {
            if (Camera.main != null)
            {
                GameObject camObj = Camera.main.gameObject;
                if (camObj.GetComponent<FlickGrabInteractor>() == null)
                {
                    camObj.AddComponent<FlickGrabInteractor>();
                }
            }
            else
            {
                Debug.LogError("[FlickGrab] Could not find Main Camera to auto-setup FlickGrabInteractor.");
            }
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

            if (cube.GetComponent<Collider>() == null) cube.AddComponent<BoxCollider>();

            // Add XRI
            if (!cube.GetComponent<XRGrabInteractable>())
            {
                cube.AddComponent<XRGrabInteractable>();
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
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                
                Material mat = new Material(shader);
                mat.color = cubeColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cubeColor);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                renderer.material = mat;
            }

            // Ensure it's on a layer the interactor can hit (Default is usually 0)
            cube.layer = 0;

            Debug.Log($"[FlickGrab] Spawned cube at {cube.transform.position} on layer {LayerMask.LayerToName(cube.layer)}");
        }
    }
}
