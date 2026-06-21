using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// GazeReticle — VR gaze cursor for PICO 4 / XR Device Simulator testing.
///
/// Auto-attaches itself to the XR Origin at runtime.
///
/// Shows a small ring/dot at the centre of the screen that:
///   • Changes colour when hovering over a clickable UI button
///   • Optionally auto-clicks after a dwell timer (gaze-select)
///
/// Works alongside DesktopMouseClicker: in the editor you see the reticle
/// AND can left-click. On a real PICO 4 headset the reticle gives gaze
/// feedback while controller rays handle clicking.
/// </summary>
[DisallowMultipleComponent]
public class GazeReticle : MonoBehaviour
{
    [Header("Appearance")]
    [Tooltip("Radius of the reticle ring in screen pixels.")]
    public float reticleRadius = 18f;

    [Tooltip("Colour when not hovering anything interactable.")]
    public Color idleColor = new Color(1f, 1f, 1f, 0.75f);

    [Tooltip("Colour when hovering a button / selectable.")]
    public Color hoverColor = new Color(0.2f, 1f, 0.5f, 1f);

    [Tooltip("Width of the ring stroke in pixels.")]
    public float ringWidth = 3f;

    [Header("Dwell Click (optional)")]
    [Tooltip("If > 0, auto-clicks the hovered button after this many seconds of looking at it.")]
    public float dwellTime = 0f;

    // ── Internal ──────────────────────────────────────────────────────────────
    private Camera _cam;
    private bool   _hovering;
    private string _hoverName = "";
    private float  _dwellTimer;
    private Button _dwellTarget;

    // Texture used to draw the ring
    private Texture2D _ringTex;
    private const int TEX_SIZE = 64;

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
            Debug.LogWarning("[GazeReticle] Could not find XR Origin — reticle disabled.");
            return;
        }

        if (origin.GetComponent<GazeReticle>() == null)
        {
            origin.AddComponent<GazeReticle>();
            Debug.Log($"[GazeReticle] Attached to '{origin.name}'.");
        }
    }

    // ── MonoBehaviour ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _cam = Camera.main ?? GetComponentInChildren<Camera>();
        BuildRingTexture();
    }

    private void Update()
    {
        if (_cam == null) { _cam = Camera.main; return; }

        DetectGazeTarget();
        HandleDwell();
    }

    // ── Gaze detection (centre-screen ray) ───────────────────────────────────
    private void DetectGazeTarget()
    {
        _hovering  = false;
        _hoverName = "";

        Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // 1. EventSystem screen-space check
        if (EventSystem.current != null)
        {
            var pd = new PointerEventData(EventSystem.current) { position = centre };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pd, results);

            foreach (var r in results)
            {
                var btn = r.gameObject.GetComponentInParent<Button>();
                if (btn != null && btn.interactable)
                {
                    _hovering  = true;
                    _hoverName = btn.name;
                    if (dwellTime > 0f) _dwellTarget = btn;
                    return;
                }
                var sel = r.gameObject.GetComponentInParent<Selectable>();
                if (sel != null && sel.interactable)
                {
                    _hovering  = true;
                    _hoverName = sel.name;
                    return;
                }
            }
        }

        // 2. Fallback: world-space ray from camera centre
        Ray ray = _cam.ScreenPointToRay(centre);
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);

        foreach (var canvas in canvases)
        {
            if (canvas.renderMode != RenderMode.WorldSpace) continue;

            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.raycastTarget) continue;
                var btn = graphic.GetComponentInParent<Button>();
                if (btn == null || !btn.interactable) continue;

                var rt = graphic.rectTransform;
                Plane plane = new Plane(-rt.forward, rt.position);
                if (plane.Raycast(ray, out float dist))
                {
                    Vector3 worldHit = ray.GetPoint(dist);
                    Vector2 localHit = rt.InverseTransformPoint(worldHit);
                    if (rt.rect.Contains(localHit))
                    {
                        _hovering    = true;
                        _hoverName   = btn.name;
                        _dwellTarget = btn;
                        return;
                    }
                }
            }
        }

        // Not hovering — reset dwell
        _dwellTarget = null;
        _dwellTimer  = 0f;
    }

    private void HandleDwell()
    {
        if (dwellTime <= 0f || !_hovering || _dwellTarget == null) return;

        _dwellTimer += Time.deltaTime;
        if (_dwellTimer >= dwellTime)
        {
            Debug.Log($"[GazeReticle] Dwell-click on '{_dwellTarget.name}'");
            _dwellTarget.onClick.Invoke();
            _dwellTimer  = 0f;
            _dwellTarget = null;
        }
    }

    // ── GUI — Draw reticle ring ───────────────────────────────────────────────
    private void OnGUI()
    {
        if (_ringTex == null) BuildRingTexture();

        float cx = Screen.width  * 0.5f;
        float cy = Screen.height * 0.5f;
        float size = reticleRadius * 2f;

        Color c = _hovering ? hoverColor : idleColor;
        GUI.color = c;
        GUI.DrawTexture(
            new Rect(cx - reticleRadius, cy - reticleRadius, size, size),
            _ringTex, ScaleMode.ScaleToFit, true);
        GUI.color = Color.white;

        // Label when hovering
        if (_hovering && !string.IsNullOrEmpty(_hoverName))
        {
            var style = new GUIStyle();
            style.fontSize  = 13;
            style.alignment = TextAnchor.UpperCenter;
            style.normal.textColor = hoverColor;
            style.fontStyle = FontStyle.Bold;

            string label = $"[ {_hoverName} ]";
            GUI.Label(new Rect(cx - 120, cy + reticleRadius + 6f, 240, 22), label, style);
        }

        // Dwell progress arc indicator
        if (dwellTime > 0f && _hovering && _dwellTimer > 0f)
        {
            float progress = _dwellTimer / dwellTime;
            var barRect = new Rect(cx - reticleRadius, cy + reticleRadius + 28f, reticleRadius * 2f * progress, 4f);
            GUI.color = hoverColor;
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

    // ── Ring texture builder ──────────────────────────────────────────────────
    private void BuildRingTexture()
    {
        _ringTex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        _ringTex.filterMode = FilterMode.Bilinear;
        _ringTex.wrapMode   = TextureWrapMode.Clamp;

        float half   = TEX_SIZE * 0.5f;
        float outerR = half - 1f;
        float innerR = outerR - (ringWidth / reticleRadius) * half;

        for (int y = 0; y < TEX_SIZE; y++)
        {
            for (int x = 0; x < TEX_SIZE; x++)
            {
                float dx   = x - half;
                float dy   = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a    = Mathf.Clamp01(outerR - Mathf.Abs(dist - (outerR + innerR) * 0.5f)
                                           - (outerR - innerR) * 0.5f + 1.5f);
                _ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        _ringTex.Apply();
    }

    private void OnDestroy()
    {
        if (_ringTex != null) Destroy(_ringTex);
    }
}
