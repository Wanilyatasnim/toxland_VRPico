using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ToxlandAudioManager — Automatically injects Sound Effects (SFX) into the game at runtime.
/// - Adds a "Pop" sound to every UI Button in the scene.
/// - Adds a "Clack" sound to the Dice when it bounces.
/// 
/// Uses procedural audio generation so no external audio files or scene references are needed!
/// </summary>
[DisallowMultipleComponent]
public class ToxlandAudioManager : MonoBehaviour
{
    private static ToxlandAudioManager _instance;
    private AudioClip _uiClickClip;
    private AudioClip _diceClackClip;
    private AudioSource _uiSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        // Don't run in background/empty scenes
        if (Camera.main == null) return;
        if (_instance != null) return;
        
        var go = new GameObject("ToxlandAudioManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ToxlandAudioManager>();
        Debug.Log("[ToxlandAudioManager] Initialized: Generating procedural audio...");
    }

    private void Awake()
    {
        _uiClickClip = GenerateClickSound();
        _diceClackClip = GenerateClackSound();

        _uiSource = gameObject.AddComponent<AudioSource>();
        _uiSource.playOnAwake = false;
        _uiSource.spatialBlend = 0f; // 2D sound for UI
        _uiSource.volume = 0.8f;
    }

    private void Start()
    {
        WireAllUIButtons();
        WireDice();
    }

    // ── 1. UI Button Click Sounds ──
    private void WireAllUIButtons()
    {
        var allButtons = Resources.FindObjectsOfTypeAll<Button>();
        int count = 0;
        foreach (var btn in allButtons)
        {
            // Only attach to buttons in the active scene (skip prefabs)
            if (btn.gameObject.scene.isLoaded)
            {
                btn.onClick.AddListener(PlayUIClick);
                count++;
            }
        }
        Debug.Log($"[ToxlandAudioManager] Wired click sound to {count} UI Buttons.");
    }

    private void PlayUIClick()
    {
        if (_uiSource != null && _uiClickClip != null)
        {
            _uiSource.pitch = Random.Range(0.95f, 1.05f); // slight variation
            _uiSource.PlayOneShot(_uiClickClip);
        }
    }

    // ── 2. Dice Collision Sounds ──
    private void WireDice()
    {
        var dice = FindAnyObjectByType<Dice>();
        if (dice != null)
        {
            var ds = dice.gameObject.AddComponent<DiceSoundFX>();
            ds.clackClip = _diceClackClip;
            Debug.Log("[ToxlandAudioManager] Wired clack sound to 3D Dice.");
        }
    }

    // ── Procedural Audio Generators ──
    
    private AudioClip GenerateClickSound()
    {
        int sampleRate = 44100;
        float duration = 0.08f; 
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;
            // High pitched pop: fast sine sweep from 1200Hz to 400Hz
            float freq = Mathf.Lerp(1200f, 400f, t / duration);
            float envelope = 1f - (t / duration); // fade out
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope;
        }
        
        var clip = AudioClip.Create("UIClick", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateClackSound()
    {
        int sampleRate = 44100;
        float duration = 0.05f; 
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;
            // Clack: short burst of noise with sharp exponential decay
            float noise = Random.Range(-1f, 1f);
            float envelope = Mathf.Exp(-t * 80f); 
            samples[i] = noise * envelope;
        }
        
        var clip = AudioClip.Create("DiceClack", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

/// <summary>
/// Helper attached dynamically to the Dice to detect physical bounces
/// </summary>
public class DiceSoundFX : MonoBehaviour
{
    public AudioClip clackClip;
    private AudioSource _source;
    private Dice _dice;

    private void Awake()
    {
        _dice = GetComponent<Dice>();
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 1f; // 3D sound so it comes from the board
        _source.volume = 0.6f;
        _source.dopplerLevel = 0f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only play sound if the dice is actively rolling
        if (_dice != null && _dice.isRolling && clackClip != null)
        {
            // The faster it hits, the louder the clack
            float magnitude = collision.relativeVelocity.magnitude;
            if (magnitude > 0.5f)
            {
                float vol = Mathf.Clamp01(magnitude / 5f);
                _source.pitch = Random.Range(0.85f, 1.15f);
                _source.PlayOneShot(clackClip, vol);
            }
        }
    }
}
