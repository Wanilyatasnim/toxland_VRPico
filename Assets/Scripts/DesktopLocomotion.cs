using UnityEngine;

/// <summary>
/// Desktop WASD + mouse-look locomotion for editor / PC testing without a VR headset.
/// Automatically attaches itself to the XR Origin (VR) at startup — no manual setup needed.
///
/// Controls:
///   Right-click drag  – look around
///   W / S             – move forward / backward
///   A / D             – strafe left / right
///   Q / E             – move up / down
///   Left Shift        – sprint (2× speed)
///   Escape            – release mouse cursor
/// </summary>
[DisallowMultipleComponent]
public class DesktopLocomotion : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────
    [Header("Movement")]
    public float moveSpeed        = 3f;
    public float sprintMultiplier = 2f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;

    [Header("Optional Camera Reference")]
    [Tooltip("Camera to pitch vertically. Auto-detected from Camera.main if left blank.")]
    public Transform cameraTransform;

    // ── Internal ──────────────────────────────────────────────
    private float _pitch       = 0f;
    private bool  _mouseActive = false;

    // ── Auto-attach ───────────────────────────────────────────
    /// <summary>
    /// Called once at runtime before the first scene Update.
    /// Finds or creates the locomotion component on the XR Origin.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        // Find XR Origin (VR) by name first, then by common fallbacks
        GameObject origin = GameObject.Find("XR Origin (VR)");
        if (origin == null) origin = GameObject.Find("XR Origin");
        if (origin == null) origin = GameObject.Find("XROrigin");

        // If still not found, try to locate the main camera's top-level parent
        if (origin == null && Camera.main != null)
            origin = Camera.main.transform.root.gameObject;

        if (origin == null)
        {
            Debug.LogWarning("[DesktopLocomotion] Could not find XR Origin in the scene. WASD will not work.");
            return;
        }

        // Only add if missing
        if (origin.GetComponent<DesktopLocomotion>() == null)
        {
            var loco = origin.AddComponent<DesktopLocomotion>();
            Debug.Log($"[DesktopLocomotion] Auto-attached to '{origin.name}'. " +
                      "Right-click + drag to look, WASD to move.");
        }
    }

    // ── MonoBehaviour ─────────────────────────────────────────
    private void Awake()
    {
        // Try to resolve camera if not already set
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // Initialise pitch from current camera angle so it doesn't snap
        if (cameraTransform != null)
        {
            float euler = cameraTransform.localEulerAngles.x;
            _pitch = euler > 180f ? euler - 360f : euler;
        }
    }

    private void Update()
    {
        HandleMouseCapture();
        HandleMouseLook();
        HandleMovement();
    }

    private void HandleMouseCapture()
    {
        // Activate mouse-look on right-click press
        if (Input.GetMouseButtonDown(1))
        {
            _mouseActive = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        // Release on right-click release or Escape
        if (Input.GetMouseButtonUp(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            _mouseActive     = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }

    private void HandleMouseLook()
    {
        if (!_mouseActive) return;

        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        // Yaw the whole XR Origin around world-up
        transform.Rotate(Vector3.up, mouseX, Space.World);

        // Pitch only the camera
        if (cameraTransform != null)
        {
            _pitch  = Mathf.Clamp(_pitch - mouseY, -80f, 80f);
            Vector3 camEuler = cameraTransform.localEulerAngles;
            camEuler.x = _pitch;
            cameraTransform.localEulerAngles = camEuler;
        }
    }

    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");   // A / D
        float v = Input.GetAxis("Vertical");     // W / S

        // Nothing pressed — skip
        bool hasVertical = Mathf.Abs(v) > 0.01f;
        bool hasHorizontal = Mathf.Abs(h) > 0.01f;
        bool hasVerticalKey = Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E);

        if (!hasVertical && !hasHorizontal && !hasVerticalKey) return;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        // Keep horizontal movement flat relative to the body's yaw (ignores camera pitch)
        Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right   = transform.right;   right.y   = 0f; right.Normalize();

        Vector3 move = (forward * v + right * h) * speed * Time.deltaTime;

        // Vertical
        if (Input.GetKey(KeyCode.E)) move.y += speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q)) move.y -= speed * Time.deltaTime;

        transform.position += move;
    }

    private void OnDisable()
    {
        // Always release cursor when the component is disabled
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }
}
