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
        if (origin != null) xrCamera = origin.GetComponentInChildren<Camera>();
        else if (Camera.main != null) xrCamera = Camera.main;

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
        canvas.sortingOrder = 32000;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

        // Position 0.8 m in front of camera
        canvasGo.transform.position   = xrCamera.transform.position + xrCamera.transform.forward * 0.8f;
        canvasGo.transform.rotation   = Quaternion.LookRotation(canvasGo.transform.position - xrCamera.transform.position);
        canvasGo.transform.localScale = new Vector3(0.0012f, 0.0012f, 0.0012f);

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(800f, 650f);

        // Load default rounded sprite
        Sprite roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        // ── 5a. Pink Border Background ──
        Color pinkBorder = new Color(0.96f, 0.44f, 0.64f);
        MakeImage(canvasGo, "BorderBG", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, pinkBorder, roundedSprite, Image.Type.Sliced);

        // ── 5b. Light Yellow Inner Panel ──
        Color lightYellow = new Color(0.99f, 0.95f, 0.82f);
        var innerPanelGo = MakeImage(canvasGo, "InnerBG", Vector2.zero, new Vector2(-24f, -24f), Vector2.zero, Vector2.one, lightYellow, roundedSprite, Image.Type.Sliced);

        // ── 5c. Official Toxland Title Image ──
        var titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TITLE_IMG_PATH);
        if (titleSprite == null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TITLE_IMG_PATH);
            if (tex != null) titleSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        var logoGo = new GameObject("LogoImage");
        logoGo.transform.SetParent(innerPanelGo.transform, false);
        var logoRt = logoGo.AddComponent<RectTransform>();
        logoRt.anchorMin = new Vector2(0.5f, 1f);
        logoRt.anchorMax = new Vector2(0.5f, 1f);
        logoRt.pivot = new Vector2(0.5f, 1f);
        logoRt.anchoredPosition = new Vector2(0f, -20f);
        
        float aspect = titleSprite != null ? (float)titleSprite.texture.width / titleSprite.texture.height : 1.6f;
        float logoWidth = 650f;
        float logoHeight = logoWidth / aspect;
        logoRt.sizeDelta = new Vector2(logoWidth, logoHeight);

        var logoImg = logoGo.AddComponent<Image>();
        if (titleSprite != null)
        {
            logoImg.sprite = titleSprite;
            logoImg.preserveAspect = true;
        }

        // ── 5d. Instruction text ──
        Color brownText = new Color(0.45f, 0.30f, 0.15f);
        float textY = -20f - logoHeight - 10f; // Position right below logo
        AddTMPText(innerPanelGo, "InstructionText", new Vector2(0f, textY), new Vector2(700f, 80f), 
            "Belajar keselamatan rumah melalui permainan!\nJawab soalan dengan betul untuk markah lebih tinggi.", 
            26f, FontStyles.Bold, brownText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // ── 5e. Start Button (Green) ──
        Color greenBtn = new Color(0.48f, 0.75f, 0.26f);
        var startBtnGo = CreateStyledButton(innerPanelGo, "BtnStartGame", new Vector2(0f, 40f), new Vector2(350f, 80f), 
            "MULAKAN PERMAINAN", greenBtn, Color.white, 28f, roundedSprite);

        // ── 6. WelcomeManager ──
        var wm = canvasGo.AddComponent<WelcomeManager>();
        wm.delaySeconds = 0.2f;
        WireButton(startBtnGo, wm.StartGame);

        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorUtility.DisplayDialog("Done!", "The Toxland Welcome Popup layout has been updated!\n\nPress Play inside MainScene to test.", "OK");
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────

    private static GameObject MakeImage(GameObject parent, string name, Vector2 anchoredPos, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sprite = null, Image.Type type = Image.Type.Simple)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>();
        img.color = color;
        if (sprite != null) { img.sprite = sprite; img.type = type; }
        return go;
    }

    private static void AddTMPText(GameObject parent, string name, Vector2 anchoredPos, Vector2 sizeDelta, string text, float fontSize, FontStyles style, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot; rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.fontStyle = style; tmp.alignment = TextAlignmentOptions.Center; tmp.color = color;
    }

    private static GameObject CreateStyledButton(GameObject parent, string name, Vector2 anchoredPos, Vector2 size, string label, Color bgColor, Color textColor, float fontSize, Sprite roundedSprite)
    {
        var btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent.transform, false);
        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0f); btnRt.anchorMax = new Vector2(0.5f, 0f); btnRt.pivot = new Vector2(0.5f, 0f); btnRt.anchoredPosition = anchoredPos; btnRt.sizeDelta = size;
        var img = btnGo.AddComponent<Image>(); 
        img.color = bgColor;
        if (roundedSprite != null) { img.sprite = roundedSprite; img.type = Image.Type.Sliced; }
        
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
