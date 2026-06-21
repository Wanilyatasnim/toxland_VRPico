using UnityEngine;

/// <summary>
/// Simple desktop WASD + mouse-look locomotion for editor/PC testing.
/// Attach this to the XR Origin (VR) or a parent camera GameObject.
/// Press RIGHT-CLICK to capture mouse and look around.
/// Use WASD to move, Q/E to go up/down.
/// This script is active only in the editor or on non-VR standalone builds.
/// </summary>
public class DesktopLocomotion : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Movement speed in metres per second")]
    public float moveSpeed = 3f;

    [Tooltip("Sprint speed multiplier while holding Shift")]
    public float sprintMultiplier = 2f;

    [Header("Mouse Look")]
    [Tooltip("Mouse sensitivity")]
    public float mouseSensitivity = 2f;

    [Header("Camera Reference")]
    [Tooltip("The camera to rotate vertically. Leave blank to auto-detect Camera.main.")]
    public Transform cameraTransform;

    // Internal state
    private float _pitch = 0f;
    private bool _mouseCapture = false;

    void Start()
    {
        // Auto-find the main camera if not assigned
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        // Toggle mouse capture with right-click
        if (Input.GetMouseButtonDown(1))
        {
            _mouseCapture = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _mouseCapture = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // --- Mouse look (only when right mouse is held) ---
        if (_mouseCapture && Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Yaw the whole body (XR Origin) on the Y axis
            transform.Rotate(Vector3.up, mouseX, Space.World);

            // Pitch only the camera on its local X axis
            if (cameraTransform != null)
            {
                _pitch -= mouseY;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);
                // Only rotate the camera's X; keep the body yaw separate
                Vector3 camEuler = cameraTransform.localEulerAngles;
                camEuler.x = _pitch;
                cameraTransform.localEulerAngles = camEuler;
            }
        }
        else if (!Input.GetMouseButton(1) && _mouseCapture)
        {
            // Release capture if right mouse released
            _mouseCapture = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // --- WASD Movement ---
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        float h = Input.GetAxis("Horizontal");   // A / D
        float v = Input.GetAxis("Vertical");     // W / S

        // Move relative to the body's facing direction (ignore camera pitch for horizontal move)
        Vector3 forward = transform.forward;
        Vector3 right   = transform.right;

        // Flatten to ground plane so W always moves forward, not sky/ground
        forward.y = 0f;
        right.y   = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = (forward * v + right * h) * speed * Time.deltaTime;

        // Q / E for vertical movement
        if (Input.GetKey(KeyCode.E)) move.y += speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q)) move.y -= speed * Time.deltaTime;

        transform.position += move;
    }

    void OnDisable()
    {
        // Always release mouse when disabled
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
