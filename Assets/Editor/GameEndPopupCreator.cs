using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// GameEndPopupCreator — Editor tool that builds the Toxland Game End screen
/// as an in-game popup Canvas directly inside the MainScene.
///
/// Run via:  Tools → Toxland → Setup Game End Popup
/// </summary>
public static class GameEndPopupCreator
{
    private const string MAIN_SCENE_PATH = "Assets/Scenes/MainScene.unity";
    private const string TITLE_IMG_PATH  = "Assets/Pictures/Title.jpg";

    [MenuItem("Tools/Toxland/Setup Game End Popup", priority = 2)]
    public static void SetupGameEndPopup()
    {
        // 1. Ensure we are in MainScene
        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.name != "MainScene")
        {
            EditorUtility.DisplayDialog("Switch to MainScene?", "Please open MainScene to build the Game End popup.", "OK");
            return;
        }

        // 2. Find XR Origin Camera
        Camera xrCamera = null;
        var origin = GameObject.Find("XR Origin (VR)") ?? GameObject.Find("XR Origin") ?? GameObject.Find("XROrigin");
        if (origin != null) xrCamera = origin.GetComponentInChildren<Camera>();
        else if (Camera.main != null) xrCamera = Camera.main;

        if (xrCamera == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find XR Origin or Main Camera.", "OK");
            return;
        }

        // 3. Clean up any existing game end canvas
        var oldCanvas = GameObject.Find("GameEndCanvas");
        if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

        // 4. Create World-Space Canvas
        var canvasGo = new GameObject("GameEndCanvas");
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = xrCamera;
        canvas.sortingOrder = 32001; // render on top of welcome

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

        // ── 5c. Header text ──
        Color pinkText = new Color(0.96f, 0.2f, 0.5f);
        Color brownText = new Color(0.45f, 0.30f, 0.15f);
        
        AddTMPText(innerPanelGo, "Header", new Vector2(0f, -80f), new Vector2(700f, 80f), 
            "TAMAT!", 70f, FontStyles.Bold, pinkText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // ── 5d. Congrats text ──
        AddTMPText(innerPanelGo, "CongratsText", new Vector2(0f, -180f), new Vector2(700f, 60f), 
            "Tahniah! Anda telah mencapai petak akhir!", 
            36f, FontStyles.Bold, brownText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // ── 5e. Score display ──
        var scoreGo = AddTMPText(innerPanelGo, "ScoreText", new Vector2(0f, -320f), new Vector2(700f, 80f), 
            "Jumlah Markah: 0", 
            50f, FontStyles.Bold, pinkText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // ── 5f. Stars display ──
        Color starColor = new Color(1.0f, 0.6f, 0.1f);
        var starsGo = AddTMPText(innerPanelGo, "StarsText", new Vector2(0f, -420f), new Vector2(700f, 60f), 
            "* * *", 
            80f, FontStyles.Bold, starColor, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // ── 5g. Buttons (Replay & Quit) ──
        Color greenBtn = new Color(0.48f, 0.75f, 0.26f);
        Color redBtn   = new Color(0.90f, 0.30f, 0.30f);

        var replayBtnGo = CreateStyledButton(innerPanelGo, "BtnReplay", new Vector2(-180f, 60f), new Vector2(300f, 80f), 
            "Main Semula", greenBtn, Color.white, 28f, roundedSprite);

        var quitBtnGo = CreateStyledButton(innerPanelGo, "BtnQuit", new Vector2(180f, 60f), new Vector2(300f, 80f), 
            "Keluar", redBtn, Color.white, 28f, roundedSprite);

        // Hide by default
        canvasGo.SetActive(false);

        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorUtility.DisplayDialog("Done!", "The Toxland Game End Popup has been built.\n\nIt is hidden by default. GameEndManager will show it when the game is won.", "OK");
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

    private static GameObject AddTMPText(GameObject parent, string name, Vector2 anchoredPos, Vector2 sizeDelta, string text, float fontSize, FontStyles style, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot; rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.fontStyle = style; tmp.alignment = TextAlignmentOptions.Center; tmp.color = color;
        return go;
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
}
