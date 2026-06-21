using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// WelcomeManager — attached to the WelcomeScene root.
/// Provides a single public method that the "Start Game" button's
/// onClick event calls to load the main board-game scene.
///
/// No Photon / network connection required.
/// </summary>
public class WelcomeManager : MonoBehaviour
{
    [Header("Scene to Load")]
    [Tooltip("Exact scene name in Build Settings (case-sensitive).")]
    public string gameSceneName = "MainScene";

    [Header("Optional Fade")]
    [Tooltip("Seconds to wait before loading (allows button animation to play).")]
    public float delaySeconds = 0.4f;

    private bool _loading = false;

    // ── Public button callback ────────────────────────────────────────────────

    /// <summary>
    /// Call this from the Start Game button's onClick event.
    /// </summary>
    public void StartGame()
    {
        if (_loading) return;
        _loading = true;

        Debug.Log("[WelcomeManager] Starting game — hiding welcome popup.");

        if (delaySeconds > 0f)
            Invoke(nameof(HidePopup), delaySeconds);
        else
            HidePopup();
    }

    /// <summary>
    /// Immediately quits to desktop (for a Quit button if present).
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("[WelcomeManager] Quit requested.");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void HidePopup()
    {
        gameObject.SetActive(false);
    }
}
