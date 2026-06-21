using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
using System.Linq;

/// <summary>
/// WelcomeSceneCreator — Editor tool that builds WelcomeScene for Toxland VR.
/// Theme: bright sky blue + rainbow border + hot-pink title — matches the
/// game's existing art style (see TAMAT.jpg / AB06.jpg).
///
/// Run via:  Tools → Toxland → Setup Welcome Scene
///
/// What it does:
///   1. Creates Assets/Scenes/WelcomeScene.unity
///   2. Instantiates the XR Origin (XR Rig) prefab so controllers are visible
///   3. Builds a world-space canvas in Toxland colour scheme
///   4. Wires WelcomeManager Start/Quit buttons
///   5. Adds WelcomeScene [0] + MainScene [1] to Build Settings
/// </summary>
public static class WelcomeSceneCreator
{
    private const string WELCOME_SCENE_PATH = "Assets/Scenes/WelcomeScene.unity";
    private const string MAIN_SCENE_PATH    = "Assets/Scenes/MainScene.unity";
    private const string XR_ORIGIN_PREFAB   =
        "Assets/Samples/XR Interaction Toolkit/3.0.4/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

    // ── Toxland colour palette ────────────────────────────────────────────────
    // Derived from TAMAT.jpg / AB06.jpg art assets
    private static readonly Color SkyBlue      = new Color(0.20f, 0.75f, 1.00f); // background
    private static readonly Color HotPink      = new Color(0.93f, 0.11f, 0.52f); // title bar & main btn
    private static readonly Color BrightYellow = new Color(1.00f, 0.87f, 0.00f); // stars / accents
    private static readonly Color PanelBlue    = new Color(0.30f, 0.82f, 1.00f); // inner panel
    private static readonly Color White        = Color.white;
    private static readonly Color DarkText     = new Color(0.10f, 0.05f, 0.20f); // dark purple-black

    // Rainbow stripe colours (top/bottom bars, 7 colours)
    private static readonly Color[] Rainbow = {
        new Color(0.94f, 0.17f, 0.17f), // red
        new Color(1.00f, 0.55f, 0.00f), // orange
        new Color(1.00f, 0.87f, 0.00f), // yellow
        new Color(0.13f, 0.78f, 0.22f), // green
        new Color(0.10f, 0.55f, 0.95f), // blue
        new Color(0.50f, 0.10f, 0.85f), // purple
        new Color(0.93f, 0.11f, 0.52f), // hot pink
    };

    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Toxland/Setup Welcome Scene", priority = 1)]
    public static void SetupWelcomeScene()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Setup Welcome Scene",
            "This will create 'Assets/Scenes/WelcomeScene.unity' and update Build Settings.\n\nContinue?",
            "Yes, create it!", "Cancel");
        if (!proceed) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Directional Light ──────────────────────────────────────────────
        var lightGo = new GameObject("Directional Light");
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var lc = lightGo.AddComponent<Light>();
        lc.type      = LightType.Directional;
        lc.color     = new Color(1f, 0.97f, 0.92f);
        lc.intensity = 1.1f;

        // ── 2. XR Origin (XR Rig) — gives controllers + simulator support ─────
        Camera xrCamera    = null;
        GameObject xrOriginGo = null;

        var xrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XR_ORIGIN_PREFAB);
        if (xrPrefab != null)
        {
            xrOriginGo = (GameObject)PrefabUtility.InstantiatePrefab(xrPrefab);
            xrOriginGo.name = "XR Origin (VR)";
            xrOriginGo.transform.position = Vector3.zero;
            xrCamera = xrOriginGo.GetComponentInChildren<Camera>();
            if (xrCamera != null)
            {
                xrCamera.backgroundColor = SkyBlue;
                xrCamera.clearFlags      = CameraClearFlags.SolidColor;
                xrCamera.fieldOfView     = 90f;
            }
            Debug.Log("[WelcomeSceneCreator] XR Origin (XR Rig) instantiated from prefab.");
        }
        else
        {
            // Fallback: plain camera if prefab not found
            Debug.LogWarning($"[WelcomeSceneCreator] XR Origin prefab not found at '{XR_ORIGIN_PREFAB}'. Using plain camera.");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            xrCamera  = camGo.AddComponent<Camera>();
            xrCamera.backgroundColor = SkyBlue;
            xrCamera.clearFlags      = CameraClearFlags.SolidColor;
            xrCamera.fieldOfView     = 90f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 1.6f, 0f);
        }

        // ── 3. EventSystem + XRUIInputModule ─────────────────────────────────
        // Check if the XR Origin already contains an EventSystem
        var existingES = Object.FindFirstObjectByType<EventSystem>();
        GameObject esGo;
        XRUIInputModule xrui;
        if (existingES == null)
        {
            esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            xrui = esGo.AddComponent<XRUIInputModule>();
        }
        else
        {
            esGo = existingES.gameObject;
            xrui = esGo.GetComponent<XRUIInputModule>() ?? esGo.AddComponent<XRUIInputModule>();
        }

        // Apply the XRI preset
        string[] presetGuids = AssetDatabase.FindAssets("XRI Default XR UI Input Module t:Preset");
        if (presetGuids.Length > 0)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[0]);
            var preset = AssetDatabase.LoadAssetAtPath<UnityEditor.Presets.Preset>(presetPath);
            if (preset != null && preset.CanBeAppliedTo(xrui))
            {
                preset.ApplyTo(xrui);
                Debug.Log("[WelcomeSceneCreator] Applied XRI Default XR UI Input Module preset.");
            }
        }

        // ── 4. World-Space Canvas — Toxland theme ─────────────────────────────
        var canvasGo = new GameObject("WelcomeCanvas");
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = xrCamera;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // TrackedDeviceGraphicRaycaster — works with XR ray interactors
        // (inherits BaseRaycaster; DesktopMouseClicker now searches Canvas directly)
        canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

        // Position 2.5 m in front of XR Origin camera
        Vector3 camPos = xrCamera != null ? xrCamera.transform.position : new Vector3(0, 1.6f, 0);
        canvasGo.transform.position   = camPos + new Vector3(0f, 0f, 2.5f);
        canvasGo.transform.rotation   = Quaternion.identity;
        canvasGo.transform.localScale = new Vector3(0.006f, 0.006f, 0.006f);

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 600f);

        // ── 4a. Sky-blue background ───────────────────────────────────────────
        MakeImage(canvasGo, "BG",
                  Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one,
                  SkyBlue);

        // ── 4b. Top rainbow bar ───────────────────────────────────────────────
        BuildRainbowBar(canvasGo, "TopRainbow",
                        anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                        pivot: new Vector2(0.5f, 1f), height: 28f, yOffset: 0f);

        // ── 4c. Bottom rainbow bar ────────────────────────────────────────────
        BuildRainbowBar(canvasGo, "BotRainbow",
                        anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                        pivot: new Vector2(0.5f, 0f), height: 28f, yOffset: 0f);

        // ── 4d. Hot-pink title bar ────────────────────────────────────────────
        var titleBarGo = MakeImage(canvasGo, "TitleBar",
            new Vector2(0, -28), new Vector2(0, 90),
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            HotPink, isPivot: true, pivot: new Vector2(0.5f, 1f));

        // White border under title bar
        MakeImage(canvasGo, "TitleBarBorder",
            new Vector2(0, -118), new Vector2(0, 8),
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            White, isPivot: true, pivot: new Vector2(0.5f, 1f));

        // ── 4e. Title text — "TOXLAND VR" ─────────────────────────────────────
        var titleGo   = new GameObject("TitleText");
        titleGo.transform.SetParent(canvasGo.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin         = new Vector2(0.5f, 1f);
        titleRect.anchorMax         = new Vector2(0.5f, 1f);
        titleRect.pivot             = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition  = new Vector2(0f, -32f);
        titleRect.sizeDelta         = new Vector2(860f, 82f);
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text      = "TOXLAND VR";
        titleTmp.fontSize  = 72f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color     = White;

        // ── 4f. "Hop & Help" subtitle ─────────────────────────────────────────
        AddTMPText(canvasGo, "SubtitleText",
                   new Vector2(0f, -140f), new Vector2(840f, 48f),
                   "Hop & Help  —  Permainan Ular & Tangga VR",
                   36f, FontStyles.Bold, BrightYellow);

        // ── 4g. Inner panel (light blue card) ────────────────────────────────
        MakeImageRect(canvasGo, "InnerPanel",
                      new Vector2(0f, -30f), new Vector2(820f, 280f),
                      PanelBlue, anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                      pivot: new Vector2(0.5f, 0.5f));

        // Inner panel white border
        MakeImageRect(canvasGo, "InnerPanelBorder",
                      new Vector2(0f, -30f), new Vector2(836f, 296f),
                      White, anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                      pivot: new Vector2(0.5f, 0.5f));
        // Push border behind panel (siblings order — border created first = behind)
        var border = canvasGo.transform.Find("InnerPanelBorder");
        if (border != null) border.SetSiblingIndex(canvasGo.transform.Find("InnerPanel").GetSiblingIndex());

        // ── 4h. Instruction text ──────────────────────────────────────────────
        AddTMPText(canvasGo, "InstructionText",
                   new Vector2(0f, -10f), new Vector2(780f, 100f),
                   "Belajar keselamatan rumah melalui permainan!\nJawab soalan dengan betul untuk markah lebih tinggi.",
                   26f, FontStyles.Normal, DarkText,
                   anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f));

        // Stars decoration
        AddTMPText(canvasGo, "StarsText",
                   new Vector2(0f, -90f), new Vector2(600f, 52f),
                   "★  ★  ★  ★  ★",
                   32f, FontStyles.Normal, BrightYellow,
                   anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f));

        // ── 4i. MULAKAN PERMAINAN button (big pink) ───────────────────────────
        var startBtnGo = CreateStyledButton(canvasGo, "BtnStartGame",
                                            new Vector2(0f, -200f), new Vector2(480f, 78f),
                                            "▶   MULAKAN PERMAINAN",
                                            HotPink, White, 32f);

        // ── 4j. KELUAR button (smaller, yellow) ───────────────────────────────
        var quitBtnGo  = CreateStyledButton(canvasGo, "BtnQuit",
                                            new Vector2(0f, -290f), new Vector2(220f, 52f),
                                            "✕  Keluar",
                                            new Color(0.85f, 0.20f, 0.20f), White, 24f);

        // ── 5. WelcomeManager ─────────────────────────────────────────────────
        var wm = canvasGo.AddComponent<WelcomeManager>();
        wm.gameSceneName = "MainScene";
        wm.delaySeconds  = 0.3f;

        // Wire buttons → WelcomeManager
        WireButton(startBtnGo, wm.StartGame);
        WireButton(quitBtnGo,  wm.QuitGame);

        // ── 6. Version label (bottom) ─────────────────────────────────────────
        AddTMPText(canvasGo, "VersionLabel",
                   new Vector2(0f, 38f), new Vector2(0f, 30f),
                   "Toxland VR v1.0   |   PICO 4   |   Unity 6",
                   15f, FontStyles.Normal, new Color(0.05f, 0.3f, 0.5f, 0.9f),
                   anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                   pivot: new Vector2(0.5f, 0f));

        // ── 7. Save scene ─────────────────────────────────────────────────────
        bool saved = EditorSceneManager.SaveScene(scene, WELCOME_SCENE_PATH);
        if (!saved)
        {
            EditorUtility.DisplayDialog("Error", "Failed to save WelcomeScene.unity!", "OK");
            return;
        }
        AssetDatabase.Refresh();
        Debug.Log($"[WelcomeSceneCreator] WelcomeScene saved at '{WELCOME_SCENE_PATH}'.");

        // ── 8. Build Settings ─────────────────────────────────────────────────
        UpdateBuildSettings();

        EditorUtility.DisplayDialog(
            "✅ Done!",
            "WelcomeScene created with Toxland theme and added to Build Settings.\n\n" +
            "Build order:\n  [0] WelcomeScene\n  [1] MainScene\n\n" +
            "Open WelcomeScene and press Play to test!",
            "Awesome!");
    }

    // ── Build Settings ────────────────────────────────────────────────────────
    private static void UpdateBuildSettings()
    {
        var existing = EditorBuildSettings.scenes.ToList();
        bool hasWelcome = existing.Any(s => s.path == WELCOME_SCENE_PATH);
        bool hasMain    = existing.Any(s => s.path == MAIN_SCENE_PATH);

        var next = new System.Collections.Generic.List<EditorBuildSettingsScene>();

        if (!hasWelcome) next.Add(new EditorBuildSettingsScene(WELCOME_SCENE_PATH, true));
        else             { var w = existing.First(s => s.path == WELCOME_SCENE_PATH); w.enabled = true; next.Add(w); existing.RemoveAll(s => s.path == WELCOME_SCENE_PATH); }

        if (!hasMain)    next.Add(new EditorBuildSettingsScene(MAIN_SCENE_PATH, true));
        else             { var m = existing.First(s => s.path == MAIN_SCENE_PATH);    m.enabled = true; next.Add(m); existing.RemoveAll(s => s.path == MAIN_SCENE_PATH); }

        foreach (var s in existing) next.Add(s);
        EditorBuildSettings.scenes = next.ToArray();
        Debug.Log("[WelcomeSceneCreator] Build Settings updated: WelcomeScene[0], MainScene[1].");
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────

    /// <summary>Creates a full-canvas-stretch or anchor-based Image.</summary>
    private static GameObject MakeImage(GameObject parent, string name,
                                         Vector2 anchoredPos, Vector2 sizeDelta,
                                         Vector2 anchorMin, Vector2 anchorMax,
                                         Color color,
                                         bool isPivot = false, Vector2 pivot = default)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt   = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        if (isPivot) rt.pivot = pivot;
        go.AddComponent<Image>().color = color;
        return go;
    }

    /// <summary>Creates an Image with centre-anchor positioning.</summary>
    private static GameObject MakeImageRect(GameObject parent, string name,
                                             Vector2 anchoredPos, Vector2 size, Color color,
                                             Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;
        go.AddComponent<Image>().color = color;
        return go;
    }

    /// <summary>Builds a horizontal row of rainbow colour strips.</summary>
    private static void BuildRainbowBar(GameObject parent, string name,
                                         Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 pivot, float height, float yOffset)
    {
        var barGo = new GameObject(name);
        barGo.transform.SetParent(parent.transform, false);
        var barRt = barGo.AddComponent<RectTransform>();
        barRt.anchorMin        = anchorMin;
        barRt.anchorMax        = anchorMax;
        barRt.pivot            = pivot;
        barRt.anchoredPosition = new Vector2(0, yOffset);
        barRt.sizeDelta        = new Vector2(0, height);

        // Transparent image as container (needed for RectTransform)
        var barImg = barGo.AddComponent<Image>();
        barImg.color = Color.clear;

        int count      = Rainbow.Length;
        float stripW   = 900f / count; // canvas is 900 units wide

        for (int i = 0; i < count; i++)
        {
            var strip = new GameObject($"Strip{i}");
            strip.transform.SetParent(barGo.transform, false);
            var srt = strip.AddComponent<RectTransform>();
            srt.anchorMin        = new Vector2(0f, 0f);
            srt.anchorMax        = new Vector2(0f, 1f);
            srt.pivot            = new Vector2(0f, 0f);
            srt.anchoredPosition = new Vector2(i * stripW - 450f, 0f); // offset from centre
            srt.sizeDelta        = new Vector2(stripW + 1f, 0f); // +1 to avoid gaps
            strip.AddComponent<Image>().color = Rainbow[i];
        }
    }

    /// <summary>Adds a TextMeshProUGUI child.</summary>
    private static void AddTMPText(GameObject parent, string name,
                                    Vector2 anchoredPos, Vector2 sizeDelta,
                                    string text, float fontSize, FontStyles style, Color color,
                                    Vector2 anchorMin = default, Vector2 anchorMax = default,
                                    Vector2 pivot = default,
                                    TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        bool centreAnchored = (anchorMin == default && anchorMax == default);
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = centreAnchored ? new Vector2(0.5f, 1f) : anchorMin;
        rt.anchorMax        = centreAnchored ? new Vector2(0.5f, 1f) : anchorMax;
        rt.pivot            = (pivot == default) ? new Vector2(0.5f, 0.5f) : pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color     = color;
    }

    /// <summary>Creates a styled button GameObject with label.</summary>
    private static GameObject CreateStyledButton(GameObject parent, string name,
                                                   Vector2 anchoredPos, Vector2 size,
                                                   string label, Color bgColor,
                                                   Color textColor, float fontSize)
    {
        var btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent.transform, false);
        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin        = new Vector2(0.5f, 1f);
        btnRt.anchorMax        = new Vector2(0.5f, 1f);
        btnRt.pivot            = new Vector2(0.5f, 1f);
        btnRt.anchoredPosition = anchoredPos;
        btnRt.sizeDelta        = size;

        var img = btnGo.AddComponent<Image>();
        img.color = bgColor;

        var btn = btnGo.AddComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = bgColor;
        cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.3f);
        cb.pressedColor     = Color.Lerp(bgColor, Color.black, 0.25f);
        cb.selectedColor    = cb.highlightedColor;
        btn.colors = cb;

        // Label
        var lblGo = new GameObject("Label");
        lblGo.transform.SetParent(btnGo.transform, false);
        var lblRt = lblGo.AddComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.sizeDelta = Vector2.zero;

        var tmp = lblGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = textColor;

        return btnGo;
    }

    /// <summary>Wires a UnityEvent persistent listener.</summary>
    private static void WireButton(GameObject btnGo, UnityEngine.Events.UnityAction action)
    {
        var btn = btnGo.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
    }

    // ── Diagnostic ────────────────────────────────────────────────────────────
    [MenuItem("Tools/Toxland/Check Build Settings", priority = 2)]
    private static void CheckBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        string report = $"Build Settings ({scenes.Length} scenes):\n";
        for (int i = 0; i < scenes.Length; i++)
            report += $"  [{i}] {(scenes[i].enabled ? "✅" : "❌")} {scenes[i].path}\n";
        EditorUtility.DisplayDialog("Build Settings", report, "OK");
        Debug.Log(report);
    }
}
