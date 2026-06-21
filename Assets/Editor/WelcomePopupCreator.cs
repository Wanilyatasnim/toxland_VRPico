using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
using System.Linq;

/// <summary>
/// WelcomePopupCreator — Editor tool that builds the Toxland Welcome screen
/// as an in-game popup Canvas directly inside the MainScene.
///
/// Run via:  Tools → Toxland → Setup Welcome Popup
/// </summary>
public static class WelcomePopupCreator
{
    private const string WELCOME_SCENE_PATH = "Assets/Scenes/WelcomeScene.unity";
    private const string MAIN_SCENE_PATH    = "Assets/Scenes/MainScene.unity";

    // ── Toxland colour palette ────────────────────────────────────────────────
    private static readonly Color SkyBlue      = new Color(0.20f, 0.75f, 1.00f); 
    private static readonly Color HotPink      = new Color(0.93f, 0.11f, 0.52f); 
    private static readonly Color BrightYellow = new Color(1.00f, 0.87f, 0.00f); 
    private static readonly Color PanelBlue    = new Color(0.30f, 0.82f, 1.00f); 
    private static readonly Color White        = Color.white;
    private static readonly Color DarkText     = new Color(0.10f, 0.05f, 0.20f); 

    private static readonly Color[] Rainbow = {
        new Color(0.94f, 0.17f, 0.17f), // red
        new Color(1.00f, 0.55f, 0.00f), // orange
        new Color(1.00f, 0.87f, 0.00f), // yellow
        new Color(0.13f, 0.78f, 0.22f), // green
        new Color(0.10f, 0.55f, 0.95f), // blue
        new Color(0.50f, 0.10f, 0.85f), // purple
        new Color(0.93f, 0.11f, 0.52f), // hot pink
    };

    [MenuItem("Tools/Toxland/Setup Welcome Popup", priority = 1)]
    public static void SetupWelcomePopup()
    {
        // 1. Ensure we are in MainScene
        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.name != "MainScene")
        {
            if (EditorUtility.DisplayDialog("Switch to MainScene?", 
                "The Welcome Popup should be built inside MainScene.\nWould you like to open MainScene now?", "Yes", "No"))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(MAIN_SCENE_PATH);
                else return;
            }
            else return;
        }

        // 2. Remove old WelcomeScene from build settings if it exists
        var existing = EditorBuildSettings.scenes.ToList();
        if (existing.Any(s => s.path == WELCOME_SCENE_PATH))
        {
            existing.RemoveAll(s => s.path == WELCOME_SCENE_PATH);
            EditorBuildSettings.scenes = existing.ToArray();
            Debug.Log("[WelcomePopupCreator] Removed old WelcomeScene from Build Settings.");
        }

        // 3. Find XR Origin Camera
        Camera xrCamera = null;
        var origin = GameObject.Find("XR Origin (VR)") ?? GameObject.Find("XR Origin") ?? GameObject.Find("XROrigin");
        if (origin != null)
        {
            xrCamera = origin.GetComponentInChildren<Camera>();
        }
        else if (Camera.main != null)
        {
            xrCamera = Camera.main;
        }

        if (xrCamera == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find XR Origin or Main Camera in the scene. Please ensure your VR rig is set up.", "OK");
            return;
        }

        // 4. Clean up any existing welcome canvas
        var oldCanvas = GameObject.Find("WelcomeCanvas");
        if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

        // 5. Create World-Space Canvas
        var canvasGo = new GameObject("WelcomeCanvas");
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = xrCamera;
        canvas.sortingOrder = 32000; // render on top

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

        // Position 2.5 m in front of camera
        canvasGo.transform.position   = xrCamera.transform.position + new Vector3(0f, 0f, 2.5f);
        // Face the camera
        canvasGo.transform.rotation   = Quaternion.LookRotation(canvasGo.transform.position - xrCamera.transform.position);
        canvasGo.transform.localScale = new Vector3(0.006f, 0.006f, 0.006f);

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 600f);

        // ── 5a. Sky-blue background ──
        MakeImage(canvasGo, "BG", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, SkyBlue);

        // ── 5b. Rainbow bars ──
        BuildRainbowBar(canvasGo, "TopRainbow", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 28f, 0f);
        BuildRainbowBar(canvasGo, "BotRainbow", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 28f, 0f);

        // ── 5c. Title bar ──
        MakeImage(canvasGo, "TitleBar", new Vector2(0, -28), new Vector2(0, 90), new Vector2(0f, 1f), new Vector2(1f, 1f), HotPink, true, new Vector2(0.5f, 1f));
        MakeImage(canvasGo, "TitleBarBorder", new Vector2(0, -118), new Vector2(0, 8), new Vector2(0f, 1f), new Vector2(1f, 1f), White, true, new Vector2(0.5f, 1f));

        // ── 5d. Title text ──
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

        // ── 5e. Subtitle ──
        AddTMPText(canvasGo, "SubtitleText", new Vector2(0f, -140f), new Vector2(840f, 48f), "Hop & Help  —  Permainan Ular & Tangga VR", 36f, FontStyles.Bold, BrightYellow);

        // ── 5f. Inner panel ──
        MakeImageRect(canvasGo, "InnerPanel", new Vector2(0f, -30f), new Vector2(820f, 280f), PanelBlue, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        MakeImageRect(canvasGo, "InnerPanelBorder", new Vector2(0f, -30f), new Vector2(836f, 296f), White, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var border = canvasGo.transform.Find("InnerPanelBorder");
        if (border != null) border.SetSiblingIndex(canvasGo.transform.Find("InnerPanel").GetSiblingIndex());

        // ── 5g. Instruction text ──
        AddTMPText(canvasGo, "InstructionText", new Vector2(0f, -10f), new Vector2(780f, 100f), "Belajar keselamatan rumah melalui permainan!\nJawab soalan dengan betul untuk markah lebih tinggi.", 26f, FontStyles.Normal, DarkText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        AddTMPText(canvasGo, "StarsText", new Vector2(0f, -90f), new Vector2(600f, 52f), "★  ★  ★  ★  ★", 32f, FontStyles.Normal, BrightYellow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        // ── 5h. Buttons ──
        var startBtnGo = CreateStyledButton(canvasGo, "BtnStartGame", new Vector2(0f, -200f), new Vector2(480f, 78f), "▶   MULAKAN PERMAINAN", HotPink, White, 32f);

        // ── 6. WelcomeManager ──
        var wm = canvasGo.AddComponent<WelcomeManager>();
        wm.delaySeconds = 0.2f;

        WireButton(startBtnGo, wm.StartGame);

        EditorSceneManager.MarkSceneDirty(currentScene);

        EditorUtility.DisplayDialog("Done!", 
            "The Toxland Welcome Popup has been added directly to MainScene.\n\n" +
            "Press Play inside MainScene to test. Clicking Start will dismiss the popup and start the game immediately.", "OK");
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────

    private static GameObject MakeImage(GameObject parent, string name, Vector2 anchoredPos, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax, Color color, bool isPivot = false, Vector2 pivot = default)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt   = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        if (isPivot) rt.pivot = pivot;
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static GameObject MakeImageRect(GameObject parent, string name, Vector2 anchoredPos, Vector2 size, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot; rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static void BuildRainbowBar(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, float height, float yOffset)
    {
        var barGo = new GameObject(name);
        barGo.transform.SetParent(parent.transform, false);
        var barRt = barGo.AddComponent<RectTransform>();
        barRt.anchorMin = anchorMin; barRt.anchorMax = anchorMax; barRt.pivot = pivot; barRt.anchoredPosition = new Vector2(0, yOffset); barRt.sizeDelta = new Vector2(0, height);
        barGo.AddComponent<Image>().color = Color.clear;

        float stripW = 900f / Rainbow.Length;
        for (int i = 0; i < Rainbow.Length; i++)
        {
            var strip = new GameObject($"Strip{i}");
            strip.transform.SetParent(barGo.transform, false);
            var srt = strip.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 0f);
            srt.anchoredPosition = new Vector2(i * stripW - 450f, 0f); srt.sizeDelta = new Vector2(stripW + 1f, 0f);
            strip.AddComponent<Image>().color = Rainbow[i];
        }
    }

    private static void AddTMPText(GameObject parent, string name, Vector2 anchoredPos, Vector2 sizeDelta, string text, float fontSize, FontStyles style, Color color, Vector2 anchorMin = default, Vector2 anchorMax = default, Vector2 pivot = default, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        bool centreAnchored = (anchorMin == default && anchorMax == default);
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = centreAnchored ? new Vector2(0.5f, 1f) : anchorMin; rt.anchorMax = centreAnchored ? new Vector2(0.5f, 1f) : anchorMax;
        rt.pivot = (pivot == default) ? new Vector2(0.5f, 0.5f) : pivot; rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.fontStyle = style; tmp.alignment = alignment; tmp.color = color;
    }

    private static GameObject CreateStyledButton(GameObject parent, string name, Vector2 anchoredPos, Vector2 size, string label, Color bgColor, Color textColor, float fontSize)
    {
        var btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent.transform, false);
        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 1f); btnRt.anchorMax = new Vector2(0.5f, 1f); btnRt.pivot = new Vector2(0.5f, 1f); btnRt.anchoredPosition = anchoredPos; btnRt.sizeDelta = size;
        var img = btnGo.AddComponent<Image>(); img.color = bgColor;
        var btn = btnGo.AddComponent<Button>();
        var cb = btn.colors; cb.normalColor = bgColor; cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.3f); cb.pressedColor = Color.Lerp(bgColor, Color.black, 0.25f); cb.selectedColor = cb.highlightedColor; btn.colors = cb;
        var lblGo = new GameObject("Label"); lblGo.transform.SetParent(btnGo.transform, false);
        var lblRt = lblGo.AddComponent<RectTransform>(); lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one; lblRt.sizeDelta = Vector2.zero;
        var tmp = lblGo.AddComponent<TextMeshProUGUI>(); tmp.text = label; tmp.fontSize = fontSize; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center; tmp.color = textColor;
        return btnGo;
    }

    private static void WireButton(GameObject btnGo, UnityEngine.Events.UnityAction action)
    {
        var btn = btnGo.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
    }
}
