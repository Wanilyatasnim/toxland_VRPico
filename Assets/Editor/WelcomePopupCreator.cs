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
    private const string TITLE_IMG_PATH     = "Assets/Pictures/Title.jpg";

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

        // Position 0.8 m in front of camera (close enough to not clip the board)
        canvasGo.transform.position   = xrCamera.transform.position + xrCamera.transform.forward * 0.8f;
        // Face the camera
        canvasGo.transform.rotation   = Quaternion.LookRotation(canvasGo.transform.position - xrCamera.transform.position);
        canvasGo.transform.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f);

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 600f);

        // ── 5a. White background ──
        MakeImage(canvasGo, "BG", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, Color.white);

        // ── 5b. Official Toxland Title Image ──
        var titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TITLE_IMG_PATH);
        if (titleSprite == null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TITLE_IMG_PATH);
            if (tex != null)
            {
                // Create a temporary sprite if it's not configured as a sprite
                titleSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

        var logoGo = new GameObject("LogoImage");
        logoGo.transform.SetParent(canvasGo.transform, false);
        var logoRt = logoGo.AddComponent<RectTransform>();
        logoRt.anchorMin = new Vector2(0.5f, 1f);
        logoRt.anchorMax = new Vector2(0.5f, 1f);
        logoRt.pivot = new Vector2(0.5f, 1f);
        logoRt.anchoredPosition = new Vector2(0f, -40f);
        // Maintain aspect ratio of the title image
        float aspect = titleSprite != null ? (float)titleSprite.texture.width / titleSprite.texture.height : 1.6f;
        logoRt.sizeDelta = new Vector2(800f, 800f / aspect);

        var logoImg = logoGo.AddComponent<Image>();
        if (titleSprite != null)
        {
            logoImg.sprite = titleSprite;
            logoImg.preserveAspect = true;
        }
        else
        {
            logoImg.color = Color.gray;
            AddTMPText(logoGo, "MissingText", Vector2.zero, new Vector2(800f, 100f), "Logo Missing", 40f, FontStyles.Bold, Color.red);
        }

        // ── 5c. Instruction text ──
        AddTMPText(canvasGo, "InstructionText", new Vector2(0f, -40f), new Vector2(780f, 100f), "Belajar keselamatan rumah melalui permainan!\nJawab soalan dengan betul untuk markah lebih tinggi.", 28f, FontStyles.Normal, new Color(0.2f, 0.2f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        // ── 5d. Start Button (Blue) ──
        var startBtnGo = CreateStyledButton(canvasGo, "BtnStartGame", new Vector2(0f, -220f), new Vector2(480f, 80f), "▶   MULAKAN PERMAINAN", new Color(0.15f, 0.55f, 0.95f), Color.white, 32f);

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
