using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Polls CircuitLab public state at ~5 Hz and triggers game-feel VFX:
///  (a) New wire/component connections → SparkAt at the component position.
///  (b) Bulb current drop to zero (proxy for filament blown) → SmokeAt + audio.
/// Also attaches BulbGlow to each Bulb at runtime for smooth emission ramping.
/// Self-initializing via RuntimeInitializeOnLoadMethod — no scene edits required.
/// </summary>
public class CircuitJuiceMonitor : MonoBehaviour
{
    private const float PollInterval = 0.2f;   // 5 Hz
    private const float ScanInterval = 2f;     // Re-scan for new components every 2 s
    private const double SignificantCurrent = 0.0000001;

    [Header("Audio Clips")]
    [Tooltip("Played when a bulb filament blows (current drops to zero).")]
    [SerializeField] private AudioClip _fizzleClip;
    [Tooltip("Alternative clip for bulb blow events.")]
    [SerializeField] private AudioClip _popClip;
    [Tooltip("Played when a component is newly placed on the breadboard.")]
    [SerializeField] private AudioClip _circuitClickClip;

    private float _pollTimer;
    private float _scanTimer;

    private readonly Dictionary<CircuitComponent, bool> _placedState = new Dictionary<CircuitComponent, bool>();
    private readonly Dictionary<Bulb, double> _bulbCurrents = new Dictionary<Bulb, double>();
    private readonly HashSet<Bulb> _glowAttached = new HashSet<Bulb>();

    private CircuitComponent[] _components = System.Array.Empty<CircuitComponent>();
    private Bulb[] _bulbs = System.Array.Empty<Bulb>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        // Only create if a CircuitLab exists in the scene
        var lab = Object.FindAnyObjectByType<CircuitLab>();
        if (lab == null) return;

        var go = new GameObject("CircuitJuiceMonitor");
        var monitor = go.AddComponent<CircuitJuiceMonitor>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        // Load audio clips from Assets/Sounds/ — try AssetDatabase in editor, fall back gracefully
        LoadAudioClips();
    }

    private void LoadAudioClips()
    {
#if UNITY_EDITOR
        _fizzleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Fizzle.wav");
        _popClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Pop.wav");
        _circuitClickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/CircuitClick.wav");
#endif
    }

    private void Start()
    {
        ScanComponents();
    }

    private void Update()
    {
        _scanTimer += Time.deltaTime;
        _pollTimer += Time.deltaTime;

        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0f;
            ScanComponents();
        }

        if (_pollTimer >= PollInterval)
        {
            _pollTimer = 0f;
            PollState();
        }
    }

    private void ScanComponents()
    {
        _components = Object.FindObjectsByType<CircuitComponent>(FindObjectsInactive.Exclude);
        _bulbs = Object.FindObjectsByType<Bulb>(FindObjectsInactive.Exclude);

        // Attach BulbGlow to any bulbs that don't have it yet
        foreach (var bulb in _bulbs)
        {
            if (bulb == null) continue;
            if (_glowAttached.Contains(bulb)) continue;
            if (bulb.GetComponent<BulbGlow>() == null)
            {
                bulb.gameObject.AddComponent<BulbGlow>();
            }
            _glowAttached.Add(bulb);
        }

        // Clean up destroyed bulbs from tracking
        _glowAttached.RemoveWhere(b => b == null);
    }

    private void PollState()
    {
        // (a) Detect new component placements → spark
        foreach (var comp in _components)
        {
            if (comp == null) continue;

            bool currentlyPlaced = comp.IsPlaced;
            if (!_placedState.TryGetValue(comp, out bool wasPlaced))
            {
                _placedState[comp] = currentlyPlaced;
                continue;
            }

            if (!wasPlaced && currentlyPlaced)
            {
                // New connection — spark at the component's world position
                Vector3 pos = comp.transform.position;
                JuiceManager.SparkAt(pos);

                if (_circuitClickClip != null)
                    AudioSource.PlayClipAtPoint(_circuitClickClip, pos, 0.7f);
            }

            _placedState[comp] = currentlyPlaced;
        }

        // Clean up destroyed components
        RemoveDestroyedKeys();

        // (b) Detect bulb current drop (proxy for filament blown) → smoke + audio
        foreach (var bulb in _bulbs)
        {
            if (bulb == null) continue;

            double current = bulb.GetCurrentValue();
            if (!_bulbCurrents.TryGetValue(bulb, out double prevCurrent))
            {
                _bulbCurrents[bulb] = current;
                continue;
            }

            bool wasActive = prevCurrent > SignificantCurrent;
            bool nowInactive = current <= SignificantCurrent;

            if (wasActive && nowInactive)
            {
                // Filament deactivated — spawn smoke puff and play sound
                Vector3 pos = bulb.transform.position;
                JuiceManager.SmokeAt(pos);

                AudioClip clip = (Random.value > 0.5f) ? _popClip : _fizzleClip;
                if (clip != null)
                    AudioSource.PlayClipAtPoint(clip, pos, 0.8f);
            }

            _bulbCurrents[bulb] = current;
        }

        // Clean up destroyed bulbs from current tracking
        RemoveDestroyedBulbs();
    }

    private void RemoveDestroyedKeys()
    {
        var keysToRemove = new List<CircuitComponent>();
        foreach (var kvp in _placedState)
        {
            if (kvp.Key == null)
                keysToRemove.Add(kvp.Key);
        }
        foreach (var key in keysToRemove)
            _placedState.Remove(key);
    }

    private void RemoveDestroyedBulbs()
    {
        var keysToRemove = new List<Bulb>();
        foreach (var kvp in _bulbCurrents)
        {
            if (kvp.Key == null)
                keysToRemove.Add(kvp.Key);
        }
        foreach (var key in keysToRemove)
            _bulbCurrents.Remove(key);
    }
}
