using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
using System.Linq;

/// <summary>
/// WelcomeSceneCreator — Editor tool that programmatically builds the
/// "WelcomeScene" for Toxland VR: Hop & Help.
///
/// Run via:  Tools → Toxland → Setup Welcome Scene
///
/// What it does:
///   1. Creates Assets/Scenes/WelcomeScene.unity with all UI components wired
///   2. Adds WelcomeScene (index 0) + MainScene (index 1) to Build Settings
///   3. Saves and reimports everything
/// </summary>
public static class WelcomeSceneCreator
{
    private const string WELCOME_SCENE_PATH = "Assets/Scenes/WelcomeScene.unity";
    private const string MAIN_SCENE_PATH    = "Assets/Scenes/MainScene.unity";

    [MenuItem("Tools/Toxland/Setup Welcome Scene", priority = 1)]
    public static void SetupWelcomeScene()
    {
        // ── Ask confirmation ──────────────────────────────────────────────────
        bool proceed = EditorUtility.DisplayDialog(
            "Setup Welcome Scene",
            "This will create 'Assets/Scenes/WelcomeScene.unity' and update Build Settings.\n\nContinue?",
            "Yes, create it!", "Cancel");

        if (!proceed) return;

        // ── Save current scene first ──────────────────────────────────────────
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // ── Create a new empty scene ──────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Directional Light ──────────────────────────────────────────────
        var light = new GameObject("Directional Light");
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var lightComp = light.AddComponent<Light>();
        lightComp.type  = LightType.Directional;
        lightComp.color = new Color(1f, 0.95f, 0.85f);
        lightComp.intensity = 1.2f;

        // ── 2. Main Camera ────────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.backgroundColor = new Color(0.05f, 0.08f, 0.15f);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.fieldOfView     = 90f;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 1.6f, 0f); // eye height

        // ── 3. EventSystem + XRUIInputModule ─────────────────────────────────
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        var xrui = esGo.AddComponent<XRUIInputModule>();

        // Apply the XRI Default XR UI Input Module preset if it exists
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

        // ── 4. World-Space Canvas ─────────────────────────────────────────────
        var canvasGo = new GameObject("WelcomeCanvas");
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = cam;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // TrackedDeviceGraphicRaycaster for XR ray interaction.
        // NOTE: TrackedDeviceGraphicRaycaster inherits GraphicRaycaster, so we only need one.
        // DesktopMouseClicker uses FindObjectsByType<GraphicRaycaster> which finds this too.
        canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

        // Position: 2m in front of camera, slightly above eye level
        canvasGo.transform.position   = new Vector3(0f, 1.7f, 2.5f);
        canvasGo.transform.rotation   = Quaternion.identity;
        canvasGo.transform.localScale = new Vector3(0.006f, 0.006f, 0.006f);

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 600f);

        // ── 4a. Background Panel ──────────────────────────────────────────────
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin    = Vector2.zero;
        bgRect.anchorMax    = Vector2.one;
        bgRect.sizeDelta    = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.08f, 0.18f, 1f);

        // ── 4b. Top accent strip ──────────────────────────────────────────────
        var stripGo   = new GameObject("TopAccent");
        stripGo.transform.SetParent(canvasGo.transform, false);
        var stripRect = stripGo.AddComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot     = new Vector2(0.5f, 1f);
        stripRect.sizeDelta = new Vector2(0f, 12f);
        stripRect.anchoredPosition = Vector2.zero;
        var stripImg = stripGo.AddComponent<Image>();
        stripImg.color = new Color(1f, 0.75f, 0.1f);

        // ── 4c. Game Title (large) ────────────────────────────────────────────
        var titleGo   = new GameObject("TitleText");
        titleGo.transform.SetParent(canvasGo.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0f, 140f);
        titleRect.sizeDelta = new Vector2(860f, 120f);
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text      = "TOXLAND VR";
        titleTmp.fontSize  = 88f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color     = new Color(1f, 0.82f, 0.1f); // gold

        // ── 4d. Subtitle ──────────────────────────────────────────────────────
        var subGo   = new GameObject("SubtitleText");
        subGo.transform.SetParent(canvasGo.transform, false);
        var subRect = subGo.AddComponent<RectTransform>();
        subRect.anchoredPosition = new Vector2(0f, 68f);
        subRect.sizeDelta = new Vector2(860f, 60f);
        var subTmp = subGo.AddComponent<TextMeshProUGUI>();
        subTmp.text      = "Hop & Help — Permainan Ular & Tangga VR";
        subTmp.fontSize  = 30f;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color     = new Color(0.75f, 0.88f, 1f);

        // ── 4e. Decorative divider ────────────────────────────────────────────
        var divGo   = new GameObject("Divider");
        divGo.transform.SetParent(canvasGo.transform, false);
        var divRect = divGo.AddComponent<RectTransform>();
        divRect.anchoredPosition = new Vector2(0f, 20f);
        divRect.sizeDelta = new Vector2(500f, 3f);
        var divImg = divGo.AddComponent<Image>();
        divImg.color = new Color(1f, 0.75f, 0.1f, 0.6f);

        // ── 4f. Instruction text ──────────────────────────────────────────────
        var instrGo   = new GameObject("InstructionText");
        instrGo.transform.SetParent(canvasGo.transform, false);
        var instrRect = instrGo.AddComponent<RectTransform>();
        instrRect.anchoredPosition = new Vector2(0f, -50f);
        instrRect.sizeDelta = new Vector2(800f, 80f);
        var instrTmp = instrGo.AddComponent<TextMeshProUGUI>();
        instrTmp.text      = "Belajar keselamatan rumah melalui permainan!\nPilih jawapan yang betul untuk markah lebih tinggi.";
        instrTmp.fontSize  = 22f;
        instrTmp.alignment = TextAlignmentOptions.Center;
        instrTmp.color     = new Color(0.8f, 0.88f, 1f, 0.9f);

        // ── 4g. START GAME Button ─────────────────────────────────────────────
        var startBtnGo   = CreateStyledButton(canvasGo, "BtnStartGame",
                                              new Vector2(0f, -160f), new Vector2(340f, 75f),
                                              "▶  MULAKAN PERMAINAN",
                                              new Color(0.15f, 0.55f, 0.95f));

        // ── 4h. QUIT Button ───────────────────────────────────────────────────
        var quitBtnGo    = CreateStyledButton(canvasGo, "BtnQuit",
                                              new Vector2(0f, -255f), new Vector2(200f, 52f),
                                              "✕  Keluar",
                                              new Color(0.55f, 0.1f, 0.1f));

        // ── 5. WelcomeManager on canvas ───────────────────────────────────────
        var wm = canvasGo.AddComponent<WelcomeManager>();
        wm.gameSceneName = "MainScene";
        wm.delaySeconds  = 0.3f;

        // Wire buttons → WelcomeManager
        var startBtn = startBtnGo.GetComponent<Button>();
        if (startBtn != null)
        {
            startBtn.onClick.RemoveAllListeners();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                startBtn.onClick,
                wm.StartGame);
        }

        var quitBtn = quitBtnGo.GetComponent<Button>();
        if (quitBtn != null)
        {
            quitBtn.onClick.RemoveAllListeners();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                quitBtn.onClick,
                wm.QuitGame);
        }

        // ── 6. Version label ──────────────────────────────────────────────────
        var verGo   = new GameObject("VersionLabel");
        verGo.transform.SetParent(canvasGo.transform, false);
        var verRect = verGo.AddComponent<RectTransform>();
        verRect.anchorMin = new Vector2(0f, 0f);
        verRect.anchorMax = new Vector2(1f, 0f);
        verRect.pivot     = new Vector2(0.5f, 0f);
        verRect.anchoredPosition = new Vector2(0f, 14f);
        verRect.sizeDelta = new Vector2(0f, 36f);
        var verTmp = verGo.AddComponent<TextMeshProUGUI>();
        verTmp.text      = "Toxland VR v1.0   |   PICO 4   |   Unity 6";
        verTmp.fontSize  = 16f;
        verTmp.alignment = TextAlignmentOptions.Center;
        verTmp.color     = new Color(0.5f, 0.55f, 0.65f, 0.7f);

        // ── 7. Save the scene ─────────────────────────────────────────────────
        bool saved = EditorSceneManager.SaveScene(scene, WELCOME_SCENE_PATH);
        if (!saved)
        {
            EditorUtility.DisplayDialog("Error", "Failed to save WelcomeScene.unity!", "OK");
            return;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[WelcomeSceneCreator] WelcomeScene saved at '{WELCOME_SCENE_PATH}'.");

        // ── 8. Update Build Settings ──────────────────────────────────────────
        UpdateBuildSettings();

        EditorUtility.DisplayDialog(
            "✅ Done!",
            "WelcomeScene created and added to Build Settings.\n\n" +
            "Build order:\n  [0] WelcomeScene\n  [1] MainScene\n\n" +
            "Press Play from WelcomeScene to test the full flow!",
            "Great!");
    }

    // ── Build Settings ────────────────────────────────────────────────────────
    private static void UpdateBuildSettings()
    {
        var existingScenes = EditorBuildSettings.scenes.ToList();

        // Check if already added
        bool hasWelcome = existingScenes.Any(s => s.path == WELCOME_SCENE_PATH);
        bool hasMain    = existingScenes.Any(s => s.path == MAIN_SCENE_PATH);

        var newScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();

        // WelcomeScene always at index 0
        if (!hasWelcome)
            newScenes.Add(new EditorBuildSettingsScene(WELCOME_SCENE_PATH, true));
        else
        {
            var ws = existingScenes.First(s => s.path == WELCOME_SCENE_PATH);
            ws.enabled = true;
            newScenes.Add(ws);
            existingScenes.RemoveAll(s => s.path == WELCOME_SCENE_PATH);
        }

        // MainScene always at index 1
        if (!hasMain)
            newScenes.Add(new EditorBuildSettingsScene(MAIN_SCENE_PATH, true));
        else
        {
            var ms = existingScenes.First(s => s.path == MAIN_SCENE_PATH);
            ms.enabled = true;
            newScenes.Add(ms);
            existingScenes.RemoveAll(s => s.path == MAIN_SCENE_PATH);
        }

        // Add any remaining scenes (Photon demos etc.) after
        foreach (var s in existingScenes)
            newScenes.Add(s);

        EditorBuildSettings.scenes = newScenes.ToArray();
        Debug.Log("[WelcomeSceneCreator] Build Settings updated: WelcomeScene[0], MainScene[1].");
    }

    // ── Button factory helper ─────────────────────────────────────────────────
    private static GameObject CreateStyledButton(GameObject parent, string name,
                                                  Vector2 anchoredPos, Vector2 size,
                                                  string label, Color bgColor)
    {
        var btnGo   = new GameObject(name);
        btnGo.transform.SetParent(parent.transform, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchoredPosition = anchoredPos;
        btnRect.sizeDelta = size;

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = bgColor;

        var btn = btnGo.AddComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = bgColor;
        cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
        cb.pressedColor     = Color.Lerp(bgColor, Color.black, 0.2f);
        btn.colors = cb;

        // Label child
        var lblGo   = new GameObject("Label");
        lblGo.transform.SetParent(btnGo.transform, false);
        var lblRect = lblGo.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.sizeDelta = Vector2.zero;

        var tmp = lblGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = size.y > 60f ? 28f : 22f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return btnGo;
    }

    // ── Diagnostic menu item ──────────────────────────────────────────────────
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
