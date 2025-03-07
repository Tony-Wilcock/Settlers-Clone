using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Input_SO inputs;
    [SerializeField] private Transform targetVisual;

    [SerializeField] private bool showTargetVisual;

    [SerializeField] private float moveSpeed = 30f;
    [SerializeField] private float dragMoveSpeed = 1f;
    [SerializeField] private float moveSmoothing = 10f;

    [SerializeField] private float rotationSpeed = 5000f;
    [SerializeField] private float dragRotateSpeed = 1f;
    [SerializeField] private float rotationSmoothing = 30f;

    [SerializeField] private float zoomSpeed = 0.2f;
    [SerializeField] private float zoomSmoothing = 4f;
    [SerializeField] private float zoomMax = 80f;
    [SerializeField] private float zoomMin = 20f;

    private CinemachineThirdPersonFollow thirdPersonFollow;
    private float cameraDistance;

    private Vector3 inputDirection;
    private Vector3 movementDirection;
    private Vector3 targetPosition;
    private bool isDraggingForMovement;
    private bool isDraggingForRotation;

    private float rotationInput; // Store rotation input from new input system
    private Vector2 mouseDelta; // Store mouse delta from new input system
    private float zoomInput;
    private Quaternion targetRotation;
    private Vector3 currentRotationVelocity;

    private void Start()
    {
        thirdPersonFollow = cinemachineCamera.GetComponent<CinemachineThirdPersonFollow>();
        cameraDistance = thirdPersonFollow.CameraDistance;
        inputs.OnMoveAction += HandleMoveAction;
        inputs.OnRotateAction += HandleLookAction;
        inputs.OnMouseDeltaAction += HandleMouseDeltaAction;
        inputs.OnZoomAction += HandleZoomAction;
    }

    private void OnDisable()
    {
        inputs.OnMoveAction -= HandleMoveAction;
        inputs.OnRotateAction -= HandleLookAction;
        inputs.OnMouseDeltaAction -= HandleMouseDeltaAction;
        inputs.OnZoomAction -= HandleZoomAction;
    }

    private void Update()
    {
        HandleIsDragging();
        HandleCameraPosition();
        HandleCameraRotation();
        HandleCameraZoom();

        Cursor.visible = !isDraggingForMovement && !isDraggingForRotation;
        Cursor.lockState = isDraggingForMovement || isDraggingForRotation ? CursorLockMode.Locked : CursorLockMode.None;

        // Toggle target visual on/off depending on showTargetVisual
        if (targetVisual != null)
        {
            targetVisual.gameObject.SetActive(showTargetVisual);
        }
    }

    private void HandleMoveAction(Vector2 moveInput)
    {
        inputDirection.x = moveInput.x; // A/D input
        inputDirection.z = moveInput.y; // W/S input
    }

    private void HandleLookAction(float rotation)
    {
        rotationInput = rotation;
    }

    private void HandleMouseDeltaAction(Vector2 delta)
    {
        mouseDelta = delta;
    }

    private void HandleZoomAction(float zoom)
    {
        zoomInput = zoom;
    }

    private void HandleCameraPosition()
    {
        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0;
        right.Normalize();

        if (isDraggingForMovement)
        {
            inputDirection.x = -mouseDelta.x * dragMoveSpeed;
            inputDirection.z = -mouseDelta.y * dragMoveSpeed;
        }
        else if (!isDraggingForMovement && inputDirection != Vector3.zero && inputs.playerInput.Player.Move.ReadValue<Vector2>() == Vector2.zero)
        {
            // Reset inputDirection when dragging stops and no WASD input is active
            inputDirection = Vector3.zero;
        }

        movementDirection = inputDirection.x * right + inputDirection.z * forward;

        if (movementDirection != Vector3.zero)
        {
            targetPosition += moveSpeed * Time.deltaTime * movementDirection;
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, moveSmoothing * Time.deltaTime);
    }

    private void HandleCameraRotation()
    {
        float yRotation = rotationInput;  

        if (isDraggingForRotation)
        {
            yRotation = mouseDelta.x * dragRotateSpeed;
        }

        // Tilt the camera upwards when zooming in and downwards when zooming out
        float xRotation = Mathf.Lerp(zoomMin, zoomMax, (cameraDistance - zoomMin) / (zoomMax - zoomMin));
        transform.localEulerAngles = new Vector3(Mathf.Lerp(transform.localEulerAngles.x, xRotation, Time.deltaTime * zoomSmoothing),
            transform.localEulerAngles.y,
            transform.localEulerAngles.z
            );

        Vector3 targetRotationEulerAngles = transform.eulerAngles + new Vector3(0, yRotation * rotationSpeed * Time.deltaTime, 0); // Calculate target rotation as Euler angles
        transform.eulerAngles = Vector3.SmoothDamp(transform.eulerAngles, targetRotationEulerAngles, ref currentRotationVelocity, rotationSmoothing * Time.deltaTime); // Smooth rotation using SmoothDamp
    }

    private void HandleCameraZoom()
    {
        if (zoomInput != 0)
        {
            cameraDistance -= zoomInput * zoomSpeed;
            cameraDistance = Mathf.Clamp(cameraDistance, zoomMin, zoomMax);
        }

        thirdPersonFollow.CameraDistance = Mathf.Lerp(thirdPersonFollow.CameraDistance, cameraDistance, Time.deltaTime * zoomSmoothing);
    }

    private void HandleIsDragging()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (isDraggingForMovement) return;
            isDraggingForRotation = true;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            isDraggingForRotation = false;
        }


        if (Input.GetMouseButtonDown(2))
        {
            isDraggingForMovement = true;
        }
        else if (Input.GetMouseButtonUp(2))
        {
            isDraggingForMovement = false;
        }
    }
}