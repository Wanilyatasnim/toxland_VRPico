using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameEndManager — manages the Game End popup canvas.
/// Automatically activates the canvas and sets the score when PlayerToken.OnGameWon is fired.
/// </summary>
[DisallowMultipleComponent]
public class GameEndManager : MonoBehaviour
{
    private static GameEndManager _instance;
    
    private GameObject _canvasGo;
    private TextMeshProUGUI _scoreText;
    private TextMeshProUGUI _starsText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        // Only attach if a PlayerToken exists in the scene
        if (FindObjectsByType<PlayerToken>(FindObjectsInactive.Include).Length == 0) return;
        if (_instance != null) return;
        
        var go = new GameObject("GameEndManager");
        _instance = go.AddComponent<GameEndManager>();
    }

    private void Awake()
    {
        // Find the pre-built canvas (even if it's inactive)
        _canvasGo = GameObject.Find("GameEndCanvas");
        if (_canvasGo == null)
        {
            // Search inactive objects
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas.name == "GameEndCanvas" && canvas.gameObject.scene.isLoaded)
                {
                    _canvasGo = canvas.gameObject;
                    break;
                }
            }
        }

        if (_canvasGo != null)
        {
            var tr = _canvasGo.transform;
            _scoreText = tr.Find("InnerBG/ScoreText")?.GetComponent<TextMeshProUGUI>();
            _starsText = tr.Find("InnerBG/StarsText")?.GetComponent<TextMeshProUGUI>();

            var replayBtn = tr.Find("InnerBG/BtnReplay")?.GetComponent<Button>();
            var quitBtn   = tr.Find("InnerBG/BtnQuit")?.GetComponent<Button>();

            if (replayBtn != null) replayBtn.onClick.AddListener(RestartGame);
            if (quitBtn != null)   quitBtn.onClick.AddListener(QuitGame);
        }
        else
        {
            Debug.LogWarning("[GameEndManager] GameEndCanvas not found in scene. Run 'Tools -> Toxland -> Setup Game End Popup'.");
        }
    }

    private void OnEnable()
    {
        PlayerToken.OnGameWon += HandleGameWon;
    }

    private void OnDisable()
    {
        PlayerToken.OnGameWon -= HandleGameWon;
    }

    private void HandleGameWon(int score)
    {
        if (_canvasGo == null) return;
        
        if (_scoreText != null) _scoreText.text = $"Jumlah Markah: {score}";
        if (_starsText != null) 
            _starsText.text = score >= 20 ? "★ ★ ★" : score >= 10 ? "★ ★ ☆" : "★ ☆ ☆";

        // Re-center canvas in front of the player's current view just in case they moved
        var cam = Camera.main;
        if (cam == null)
        {
            var origin = GameObject.Find("XR Origin (VR)") ?? GameObject.Find("XR Origin") ?? GameObject.Find("XROrigin");
            if (origin != null) cam = origin.GetComponentInChildren<Camera>();
        }

        if (cam != null)
        {
            _canvasGo.transform.position = cam.transform.position + cam.transform.forward * 0.8f;
            _canvasGo.transform.rotation = Quaternion.LookRotation(_canvasGo.transform.position - cam.transform.position);
        }

        _canvasGo.SetActive(true);
        Debug.Log($"[GameEndManager] Game won! Final score: {score}. Displaying popup.");
    }

    private void RestartGame()
    {
        if (_canvasGo != null) _canvasGo.SetActive(false);
        ScoreManager.scoreValue = 0;
        
        var token = FindAnyObjectByType<PlayerToken>();
        if (token != null) token.resetPosition();

        Debug.Log("[GameEndManager] Game restarted.");
    }

    private void QuitGame()
    {
        Debug.Log("[GameEndManager] Quit requested.");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
