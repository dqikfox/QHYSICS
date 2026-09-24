using UnityEngine;

/// <summary>
/// Attach-at-runtime component that smoothly ramps a bulb's material emission
/// with its sim power using MaterialPropertyBlock. Never instantiates materials
/// per-frame, never leaks materials.
/// </summary>
[RequireComponent(typeof(Bulb))]
public class BulbGlow : MonoBehaviour
{
    [Header("Emission Ramp")]
    [Tooltip("How fast the emission intensity lerps toward the target (per second).")]
    [SerializeField] private float _lerpSpeed = 4f;

    [Tooltip("Minimum emission exponent (off / dim).")]
    [SerializeField] private float _minIntensity = 0f;

    [Tooltip("Maximum emission exponent (full power, bloom-friendly).")]
    [SerializeField] private float _maxIntensity = 4f;

    [Tooltip("Current (A) at which the bulb is considered at full brightness.")]
    [SerializeField] private float _fullCurrentA = 0.01f;

    [Tooltip("Base emission color for the glow ramp.")]
    [SerializeField] private Color _emissionColor = new Color(1f, 0.85f, 0.4f);

    private Bulb _bulb;
    private Renderer _filamentRenderer;
    private MaterialPropertyBlock _mpb;
    private Material _filamentMaterial;
    private float _currentIntensity;
    private bool _keywordEnabled;

    private const double SignificantCurrent = 0.0000001;

    private void Awake()
    {
        _bulb = GetComponent<Bulb>();
        _mpb = new MaterialPropertyBlock();

        if (_bulb.filament != null)
        {
            _filamentRenderer = _bulb.filament.GetComponent<Renderer>();
            if (_filamentRenderer != null)
            {
                // Access the material instance once so we can manage the emission keyword
                // without per-frame instantiation. Bulb.cs already creates an instance via
                // .material access, so this returns the same cached instance.
                _filamentMaterial = _filamentRenderer.material;
            }
        }
    }

    private void Update()
    {
        if (_filamentRenderer == null)
            return;

        double current = _bulb.GetCurrentValue();
        float targetIntensity;

        if (current > SignificantCurrent)
        {
            float pct = Mathf.Clamp01((float)current / _fullCurrentA);
            targetIntensity = Mathf.Lerp(_minIntensity, _maxIntensity, pct);
        }
        else
        {
            targetIntensity = _minIntensity;
        }

        // Smooth lerp toward target
        _currentIntensity = Mathf.Lerp(_currentIntensity, targetIntensity, _lerpSpeed * Time.deltaTime);

        // Ensure emission keyword is enabled when we have intensity (Bulb.cs may have
        // disabled it on deactivation; we re-enable so the MPB-driven smooth fade works).
        if (_currentIntensity > 0.001f && !_keywordEnabled)
        {
            EnableEmissionKeyword();
        }
        else if (_currentIntensity <= 0.001f && _keywordEnabled)
        {
            // Once we've faded to near-zero, let Bulb.cs handle the keyword
            _keywordEnabled = false;
        }

        // Set emission color via MaterialPropertyBlock — no per-frame material instantiation
        Color finalColor = _emissionColor * Mathf.Pow(2f, _currentIntensity);
        _filamentRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_EmissionColor", finalColor);
        _filamentRenderer.SetPropertyBlock(_mpb);
    }

    private void EnableEmissionKeyword()
    {
        if (_filamentMaterial != null)
        {
            _filamentMaterial.EnableKeyword("_EMISSION");
            _keywordEnabled = true;
        }
    }

    private void OnDestroy()
    {
        // The material instance was created by Bulb.cs (via .material access).
        // We don't destroy it here to avoid conflicts — Unity will clean it up
        // when the GameObject is destroyed.
    }
}
