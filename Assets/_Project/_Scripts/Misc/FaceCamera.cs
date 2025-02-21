// FaceCamera.cs
using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // Find the main camera in the scene.  Important to do this in Start,
        // not Awake, as the camera might not be fully initialized in Awake.
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("FaceCamera: Could not find main camera!");
            enabled = false; // Disable the script if no camera is found.
        }
    }

    void Update()
    {
        // Only update if the GameObject is active.
        if (gameObject.activeInHierarchy && mainCamera != null)
        {
            // Make the node face the camera.
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                              mainCamera.transform.rotation * Vector3.up);

            // Alternative, simpler method (if you don't need to control up direction):
            // transform.LookAt(mainCamera.transform);
        }
    }
}