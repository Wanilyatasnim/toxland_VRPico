using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Desktop Mouse UI Clicker — for Editor / PC testing without a VR headset.
///
/// Auto-attaches itself to the XR Origin at runtime. Lets you click World Space and 
/// Screen Space UI buttons using the LEFT mouse button, just like a normal PC game.
///
/// How it works:
///   - Fires a ray from the camera through the mouse position
///   - Checks all GraphicRaycasters (both screen-space and world-space canvases)
///   - Sends Unity pointer click events to whatever it hits
///
/// Controls:
///   Left Mouse Button  — click any UI button you're hovering over
///   Right Mouse Button — hold to look around (handled by DesktopLocomotion)
/// </summary>
[DisallowMultipleComponent]
public class DesktopMouseClicker : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Max distance for world-space button ray detection.")]
    public float clickDistance = 100f;

    [Tooltip("Show a hint bar at the top of the screen.")]
    public bool showHints = true;

    // ── Internal ─────────────────────────────────────────────────────────────
    private Camera _cam;
    private string _hoverName  = "";
    private bool   _isHovering = false;

    // ── Auto-attach ───────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        GameObject origin = GameObject.Find("XR Origin (VR)")
                         ?? GameObject.Find("XR Origin")
                         ?? GameObject.Find("XROrigin");

        if (origin == null && Camera.main != null)
            origin = Camera.main.transform.root.gameObject;

        if (origin == null)
        {
            Debug.LogWarning("[DesktopMouseClicker] XR Origin not found — mouse clicking disabled.");
            return;
        }

        if (origin.GetComponent<DesktopMouseClicker>() == null)
        {
            origin.AddComponent<DesktopMouseClicker>();
            Debug.Log($"[DesktopMouseClicker] Attached to '{origin.name}'. Left-click to interact with buttons.");
        }
    }

    // ── MonoBehaviour ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _cam = Camera.main ?? GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (_cam == null) { _cam = Camera.main; return; }

        // Only act when cursor is visible (not captured by DesktopLocomotion)
        bool mouseFree = Cursor.lockState == CursorLockMode.None;
        if (!mouseFree) { _isHovering = false; return; }

        // Hover detection every frame
        DetectHover();

        // Click on left mouse button down
        if (Input.GetMouseButtonDown(0))
        {
            PerformClick();
        }
    }

    // ── Hover ─────────────────────────────────────────────────────────────────
    private void DetectHover()
    {
        _isHovering = false;
        _hoverName  = "";

        // Check EventSystem for screen-space UI first
        var pointerData = GetPointerData();
        var results = new List<RaycastResult>();
        EventSystem.current?.RaycastAll(pointerData, results);

        foreach (var r in results)
        {
            var btn = r.gameObject.GetComponentInParent<Button>();
            if (btn != null && btn.interactable)
            {
                _isHovering = true;
                _hoverName  = btn.name;
                return;
            }
            if (r.gameObject.GetComponent<IPointerClickHandler>() != null)
            {
                _isHovering = true;
                _hoverName  = r.gameObject.name;
                return;
            }
        }

        // Fallback: scan all GraphicRaycasters (world-space canvases)
        var (hit, btn2) = RaycastAllCanvases();
        if (btn2 != null)
        {
            _isHovering = true;
            _hoverName  = btn2.name;
        }
    }

    // ── Click ─────────────────────────────────────────────────────────────────
    private void PerformClick()
    {
        var pointerData = GetPointerData();
        var results = new List<RaycastResult>();
        EventSystem.current?.RaycastAll(pointerData, results);

        // Try EventSystem results first (screen-space canvases)
        foreach (var r in results)
        {
            if (TryClickObject(r.gameObject, pointerData)) return;
        }

        // Fallback: world-space canvases via GraphicRaycaster
        var (hitObj, btn) = RaycastAllCanvases();
        if (hitObj != null)
        {
            TryClickObject(hitObj, pointerData);
        }
    }

    private bool TryClickObject(GameObject go, PointerEventData pointerData)
    {
        // Send full pointer event sequence
        ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerClickHandler);

        // Also bubble up the hierarchy
        ExecuteEvents.ExecuteHierarchy(go, pointerData, ExecuteEvents.pointerClickHandler);

        // Directly invoke Button.onClick for reliability
        var btn = go.GetComponentInParent<Button>();
        if (btn != null && btn.interactable)
        {
            Debug.Log($"[DesktopMouseClicker] ✅ Clicked: '{btn.name}'");
            btn.onClick.Invoke();
            return true;
        }

        // Check if the object or parent has a toggle
        var toggle = go.GetComponentInParent<Toggle>();
        if (toggle != null && toggle.interactable)
        {
            toggle.isOn = !toggle.isOn;
            Debug.Log($"[DesktopMouseClicker] ✅ Toggled: '{toggle.name}' → {toggle.isOn}");
            return true;
        }

        return false;
    }

    // ── WorldSpace Canvas Raycast ─────────────────────────────────────────────
    private (GameObject hitObj, Button button) RaycastAllCanvases()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        var raycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        float nearest = float.MaxValue;
        Button nearestBtn = null;
        GameObject nearestObj = null;

        foreach (var raycaster in raycasters)
        {
            var canvas = raycaster.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) continue;

            // Use camera-ray to plane intersection for each button in this canvas
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.raycastTarget) continue;

                var btn = graphic.GetComponentInParent<Button>();
                if (btn == null || !btn.interactable) continue;

                // Check if the ray hits this graphic's RectTransform plane
                var rt = graphic.rectTransform;
                Plane plane = new Plane(-rt.forward, rt.position);

                if (plane.Raycast(ray, out float dist) && dist < clickDistance && dist < nearest)
                {
                    // Check if the hit point is within the rect
                    Vector3 worldHit = ray.GetPoint(dist);
                    Vector2 localHit = rt.InverseTransformPoint(worldHit);
                    
                    if (rt.rect.Contains(localHit))
                    {
                        nearest    = dist;
                        nearestBtn = btn;
                        nearestObj = graphic.gameObject;
                    }
                }
            }
        }

        return (nearestObj, nearestBtn);
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private PointerEventData GetPointerData()
    {
        return new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
    }

    // ── GUI ───────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        if (!showHints) return;
        if (Cursor.lockState != CursorLockMode.None) return;

        string hint;
        Color color;

        if (_isHovering)
        {
            hint  = $"● LEFT CLICK to press '{_hoverName}'";
            color = new Color(0f, 1f, 0.5f);
        }
        else
        {
            hint  = "Left-click UI buttons  |  Right-click + WASD to move";
            color = new Color(0.8f, 0.8f, 0.8f, 0.7f);
        }

        var style = new GUIStyle();
        style.fontSize  = 14;
        style.fontStyle = _isHovering ? FontStyle.Bold : FontStyle.Normal;
        style.normal.textColor = color;
        style.alignment = TextAnchor.MiddleCenter;

        // Shadow
        var shadow = new GUIStyle(style);
        shadow.normal.textColor = new Color(0, 0, 0, 0.6f);
        GUI.Label(new Rect(1, 6, Screen.width, 25), hint, shadow);
        GUI.Label(new Rect(0, 5, Screen.width, 25), hint, style);
    }
}
