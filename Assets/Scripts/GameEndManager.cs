using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// GameEndManager — shows a "TAMAT! / Congratulations" end-screen overlay
/// when the player reaches the final tile.
///
/// Subscribes to PlayerToken.OnGameWon (static Action<int>) — fired with the
/// final score value.
///
/// Auto-creates its own Canvas UI at runtime so no scene prefab is needed.
/// </summary>
[DisallowMultipleComponent]
public class GameEndManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    private static GameEndManager _instance;

    // ── Runtime UI references ─────────────────────────────────────────────────
    private GameObject  _overlayRoot;
    private bool        _showing = false;
    private int         _finalScore;

    // GUI style cache
    private GUIStyle _panelStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _scoreStyle;
    private GUIStyle _subStyle;
    private GUIStyle _btnStyle;
    private bool     _stylesReady;
    private Texture2D _bgTex;

    // ── Auto-attach ───────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        // Only attach in the game scene (not Welcome scene)
        if (FindObjectsByType<PlayerToken>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
            return; // No PlayerToken → not the game scene

        var go = new GameObject("GameEndManager");
        DontDestroyOnLoad(go); // survives scene reloads? No — we want scene scope
        _instance = go.AddComponent<GameEndManager>();
        Debug.Log("[GameEndManager] Auto-attached and listening for OnGameWon.");
    }

    // ── MonoBehaviour ─────────────────────────────────────────────────────────
    private void OnEnable()
    {
        PlayerToken.OnGameWon += HandleGameWon;
    }

    private void OnDisable()
    {
        PlayerToken.OnGameWon -= HandleGameWon;
    }

    private void OnDestroy()
    {
        PlayerToken.OnGameWon -= HandleGameWon;
        if (_bgTex != null) Destroy(_bgTex);
    }

    // ── Win handler ───────────────────────────────────────────────────────────
    private void HandleGameWon(int score)
    {
        if (_showing) return;
        _finalScore = score;
        _showing    = true;
        Debug.Log($"[GameEndManager] Game won! Final score: {score}");
    }

    // ── GUI ───────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        if (!_showing) return;

        EnsureStyles();

        float sw = Screen.width;
        float sh = Screen.height;

        // Full-screen dim overlay
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Central panel
        float pw = Mathf.Min(600f, sw * 0.85f);
        float ph = 380f;
        float px = (sw - pw) * 0.5f;
        float py = (sh - ph) * 0.5f;

        // Panel background
        GUI.color = new Color(0.05f, 0.08f, 0.15f, 0.97f);
        GUI.DrawTexture(new Rect(px, py, pw, ph), _bgTex);
        GUI.color = Color.white;

        // Gold top accent bar
        GUI.color = new Color(1f, 0.82f, 0.1f);
        GUI.DrawTexture(new Rect(px, py, pw, 6f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float innerX = px + 30f;
        float innerW = pw - 60f;

        // 🏆 emoji / title
        GUI.Label(new Rect(innerX, py + 28f, innerW, 60f), "🏆  TAMAT!", _titleStyle);

        // Congrats text
        GUI.Label(new Rect(innerX, py + 96f, innerW, 36f),
                  "Tahniah! Anda telah mencapai petak akhir!", _subStyle);

        // Score display
        GUI.Label(new Rect(innerX, py + 150f, innerW, 60f),
                  $"Jumlah Markah: {_finalScore}", _scoreStyle);

        // Stars row (visual)
        string stars = _finalScore >= 20 ? "★ ★ ★" : _finalScore >= 10 ? "★ ★ ☆" : "★ ☆ ☆";
        GUI.Label(new Rect(innerX, py + 218f, innerW, 44f), stars, _titleStyle);

        // Buttons
        float btnW = (pw - 90f) * 0.5f;
        float btnY = py + ph - 80f;

        // Restart button
        GUI.backgroundColor = new Color(0.15f, 0.55f, 0.95f);
        if (GUI.Button(new Rect(px + 30f, btnY, btnW, 48f), "🔄  Main Semula", _btnStyle))
        {
            RestartGame();
        }

        // Main Menu button
        GUI.backgroundColor = new Color(0.9f, 0.35f, 0.2f);
        if (GUI.Button(new Rect(px + 30f + btnW + 30f, btnY, btnW, 48f), "🏠  Menu Utama", _btnStyle))
        {
            BackToMenu();
        }

        GUI.backgroundColor = Color.white;
    }

    // ── Actions ───────────────────────────────────────────────────────────────
    private void RestartGame()
    {
        _showing = false;
        ScoreManager.scoreValue = 0;

        // Find and reset the PlayerToken
        var token = FindFirstObjectByType<PlayerToken>();
        if (token != null)
            token.resetPosition();

        Debug.Log("[GameEndManager] Game restarted.");
    }

    private void BackToMenu()
    {
        _showing = false;
        ScoreManager.scoreValue = 0;

        // Load WelcomeScene if it's in build settings, otherwise just restart
        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
        if (sceneCount > 1)
            SceneManager.LoadScene(0);
        else
            RestartGame();
    }

    // ── Style builder ─────────────────────────────────────────────────────────
    private void EnsureStyles()
    {
        if (_stylesReady) return;

        _bgTex = new Texture2D(1, 1);
        _bgTex.SetPixel(0, 0, Color.white);
        _bgTex.Apply();

        _titleStyle = new GUIStyle();
        _titleStyle.fontSize  = 42;
        _titleStyle.fontStyle = FontStyle.Bold;
        _titleStyle.alignment = TextAnchor.MiddleCenter;
        _titleStyle.normal.textColor = new Color(1f, 0.82f, 0.1f);

        _scoreStyle = new GUIStyle();
        _scoreStyle.fontSize  = 34;
        _scoreStyle.fontStyle = FontStyle.Bold;
        _scoreStyle.alignment = TextAnchor.MiddleCenter;
        _scoreStyle.normal.textColor = Color.white;

        _subStyle = new GUIStyle();
        _subStyle.fontSize  = 18;
        _subStyle.wordWrap  = true;
        _subStyle.alignment = TextAnchor.MiddleCenter;
        _subStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);

        _btnStyle = new GUIStyle(GUI.skin.button);
        _btnStyle.fontSize  = 18;
        _btnStyle.fontStyle = FontStyle.Bold;
        _btnStyle.normal.textColor = Color.white;

        _stylesReady = true;
    }
}
