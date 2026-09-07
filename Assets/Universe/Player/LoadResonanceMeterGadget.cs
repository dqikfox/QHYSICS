using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Experiments;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable MEASURE resonance meter: while near an InductionCircuit, shows classical
    /// LC resonant frequency f0=1/(2π√(LC)), ω0=2πf0, or L/C readout.
    /// Honesty: ideal SeriesInductance / SeriesCapacitance from InductionCoil —
    /// NOT real network analyzer, not swept VNA, not Q from -3dB bandwidth, not impedance analyzer.
    /// Does not modify L/C (BUILD Inductor/Capacitor own them). CLR resets energy storage (Vc + I_L).
    /// XR activate / N cycles preset; P steps backward. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadResonanceMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_ResonanceMeter";
        public const string Honesty =
            "Ideal LC resonance from classical InductionCoil.SeriesInductance / SeriesCapacitance (f0=1/(2π√LC)). NOT real network analyzer, not swept VNA, not Q-bandwidth, not impedance analyzer.";

        enum Mode { F0, Omega, Lc, Clr }

        static readonly string[] PresetLabels = { "F0", "OMEGA", "LC", "CLR" };
        static readonly Mode[] PresetModes = { Mode.F0, Mode.Omega, Mode.Lc, Mode.Clr };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay linked and sample.")]
        float connectWithinMeters = 0.85f;

        TextMeshPro _readout;
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex; // default F0
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        float _liveL;
        float _liveC;
        float _liveF0;
        float _liveOmega;
        float _glow;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];
        Mode ActiveMode => PresetModes[Mathf.Clamp(_presetIndex, 0, PresetModes.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            EnsureCircuits(force: true);
            BindNearest();
            RefreshText();
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void OnEnable()
        {
            WireGrab();
        }

        void OnDisable()
        {
            UnwireGrab();
        }

        void OnDestroy()
        {
            UnwireGrab();
            DestroyMat(ref _mat);
            DestroyMat(ref _faceMat);
        }

        static void DestroyMat(ref Material m)
        {
            if (m == null)
                return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
            m = null;
        }

        void Update()
        {
            if (Time.unscaledTime >= _cacheAt || _circuits == null)
                EnsureCircuits(force: false);

            BindNearest();
            if (_circuit == null)
                EnsureLabCircuit();

            SampleResonance();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        void SampleResonance()
        {
            _linked = false;
            _liveL = 0f;
            _liveC = 0f;
            _liveF0 = 0f;
            _liveOmega = 0f;
            if (_circuit == null)
                return;

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            if (dist > connectWithinMeters)
                return;

            _linked = true;
            var coil = _circuit.Coil;
            _liveL = coil != null ? coil.SeriesInductance : 0f;
            _liveC = coil != null ? coil.SeriesCapacitance : 0f;
            if (_liveL > 0f && _liveC > 0f)
            {
                _liveOmega = 1f / Mathf.Sqrt(_liveL * _liveC);
                _liveF0 = _liveOmega / (2f * Mathf.PI);
            }

            float glowSrc = _liveF0 > 0f ? Mathf.Log10(1f + _liveF0) : 0f;
            _glow = Mathf.Clamp01(glowSrc / 4f);
        }

        void WireGrab()
        {
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
            _grab.activated.AddListener(OnActivated);
        }

        void UnwireGrab()
        {
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
        }

        void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
        {
            Cycle(+1);
        }

        void PollDesktopCycle()
        {
            if (Time.unscaledTime < _inputCooldown)
                return;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            if ((cam.transform.position - TipWorld).sqrMagnitude > 1.44f)
                return;
            if (kb.nKey.wasPressedThisFrame)
            {
                Cycle(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                Cycle(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void Cycle(int delta)
        {
            if (PresetLabels.Length == 0)
                return;
            _presetIndex = (_presetIndex + delta) % PresetLabels.Length;
            if (_presetIndex < 0)
                _presetIndex += PresetLabels.Length;

            if (ActiveMode == Mode.Clr)
            {
                if (_circuit != null)
                    _circuit.ResetEnergyStorage();
                for (int i = 0; i < PresetModes.Length; i++)
                {
                    if (PresetModes[i] == Mode.F0)
                    {
                        _presetIndex = i;
                        break;
                    }
                }
            }

            ApplyVisual();
            RefreshText();
        }

        public void DesktopActivate(int delta) => Cycle(delta);

        void EnsureCircuits(bool force)
        {
            _cacheAt = Time.unscaledTime + 1.25f;
            if (!force && _circuits != null && _circuits.Length > 0)
            {
                for (int i = 0; i < _circuits.Length; i++)
                {
                    if (_circuits[i] != null)
                        return;
                }
            }
            _circuits = Object.FindObjectsByType<InductionCircuit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        void BindNearest()
        {
            if (_circuits == null || _circuits.Length == 0)
            {
                _circuit = null;
                return;
            }

            Vector3 tip = TipWorld;
            InductionCircuit best = null;
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < _circuits.Length; i++)
            {
                InductionCircuit c = _circuits[i];
                if (c == null || !c.isActiveAndEnabled)
                    continue;
                if (c.transform == transform || c.transform.IsChildOf(transform))
                    continue;
                float sq = (c.transform.position - tip).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = c;
                }
            }
            _circuit = best;
        }

        void EnsureLabCircuit()
        {
            if (_circuit != null)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            EnsureCircuits(force: true);
            BindNearest();
        }

        void EnsureVisual()
        {
            if (_bodyRenderer == null)
                _bodyRenderer = GetComponent<Renderer>();

            if (_mat == null && _bodyRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(sh);
                Color baseC = new Color(0.12f, 0.62f, 0.55f, 1f);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", baseC);
                _mat.color = baseC;
                if (_mat.HasProperty("_Metallic"))
                    _mat.SetFloat("_Metallic", 0.4f);
                if (_mat.HasProperty("_Smoothness"))
                    _mat.SetFloat("_Smoothness", 0.45f);
                if (_mat.HasProperty("_EmissionColor"))
                {
                    _mat.EnableKeyword("_EMISSION");
                    _mat.SetColor("_EmissionColor", Color.black);
                }
                _bodyRenderer.sharedMaterial = _mat;
            }

            Transform face = transform.Find("Face");
            if (face == null)
            {
                var faceGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                faceGo.name = "Face";
                faceGo.transform.SetParent(transform, false);
                faceGo.transform.localPosition = new Vector3(0f, 0.03f, 0.01f);
                faceGo.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                faceGo.transform.localScale = new Vector3(0.11f, 0.05f, 0.01f);
                Object.Destroy(faceGo.GetComponent<Collider>());
                var fr = faceGo.GetComponent<Renderer>();
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _faceMat = new Material(sh);
                Color faceC = new Color(0.04f, 0.12f, 0.11f, 1f);
                if (_faceMat.HasProperty("_BaseColor"))
                    _faceMat.SetColor("_BaseColor", faceC);
                _faceMat.color = faceC;
                fr.sharedMaterial = _faceMat;
            }
            else if (_faceMat == null)
            {
                var fr = face.GetComponent<Renderer>();
                if (fr != null)
                    _faceMat = fr.sharedMaterial;
            }
        }

        void ApplyVisual()
        {
            if (_mat == null)
                EnsureVisual();
            if (_mat == null || !_mat.HasProperty("_EmissionColor"))
                return;
            _mat.EnableKeyword("_EMISSION");
            Color pulse = new Color(0.25f, 1f, 0.85f, 1f) * (0.08f + 1.2f * _glow);
            if (!_linked)
                pulse = Color.black;
            _mat.SetColor("_EmissionColor", pulse);
        }

        void BuildReadout()
        {
            Transform existing = transform.Find("Readout");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("Readout");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 24f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.55f, 1f, 0.9f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "RES\nseeking circuit...";
        }

        static string FormatHenries(float henries)
        {
            float l = Mathf.Abs(henries);
            if (l < 1e-12f)
                return "0 H";
            if (l < 1e-6f)
                return (henries * 1e9f).ToString("0.###") + "nH";
            if (l < 1e-3f)
                return (henries * 1e6f).ToString("0.###") + "uH";
            if (l < 1f)
                return (henries * 1e3f).ToString("0.###") + "mH";
            return henries.ToString("0.###") + "H";
        }

        static string FormatFarads(float farads)
        {
            float c = Mathf.Abs(farads);
            if (c < 1e-15f)
                return "0 F";
            if (c < 1e-9f)
                return (farads * 1e12f).ToString("0.###") + "pF";
            if (c < 1e-6f)
                return (farads * 1e9f).ToString("0.###") + "nF";
            if (c < 1e-3f)
                return (farads * 1e6f).ToString("0.###") + "uF";
            if (c < 1f)
                return (farads * 1e3f).ToString("0.###") + "mF";
            return farads.ToString("0.###") + "F";
        }

        static string FormatHz(float hz)
        {
            float a = Mathf.Abs(hz);
            if (a < 1e-6f)
                return "---";
            if (a < 1f)
                return (hz * 1000f).ToString("0.###") + "mHz";
            if (a < 1000f)
                return hz.ToString("0.###") + "Hz";
            if (a < 1e6f)
                return (hz / 1000f).ToString("0.###") + "kHz";
            return (hz / 1e6f).ToString("0.###") + "MHz";
        }

        static string FormatOmega(float radPerSec)
        {
            float a = Mathf.Abs(radPerSec);
            if (a < 1e-6f)
                return "---";
            if (a < 1f)
                return radPerSec.ToString("0.###") + "rad/s";
            if (a < 1000f)
                return radPerSec.ToString("0.##") + "rad/s";
            if (a < 1e6f)
                return (radPerSec / 1000f).ToString("0.###") + "krad/s";
            return (radPerSec / 1e6f).ToString("0.###") + "Mrad/s";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "RES " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON circuit" : (dist > connectWithinMeters ? "far" : "near");
            bool hasLc = _liveL > 0f && _liveC > 0f;
            string line;
            switch (ActiveMode)
            {
                case Mode.Omega:
                    line = hasLc ? ("w0 " + FormatOmega(_liveOmega)) : "w0 --- (need L&C)";
                    break;
                case Mode.Lc:
                    line = "L " + FormatHenries(_liveL);
                    break;
                case Mode.Clr:
                case Mode.F0:
                default:
                    line = hasLc ? ("f0 " + FormatHz(_liveF0)) : "f0 --- (need L&C)";
                    break;
            }

            string aux;
            if (ActiveMode == Mode.Lc)
                aux = "C " + FormatFarads(_liveC);
            else if (ActiveMode == Mode.Omega)
                aux = hasLc ? ("f0 " + FormatHz(_liveF0)) : ("L " + FormatHenries(_liveL));
            else
                aux = hasLc
                    ? ("L " + FormatHenries(_liveL) + " C " + FormatFarads(_liveC))
                    : ("L " + FormatHenries(_liveL) + " C " + FormatFarads(_liveC));

            _readout.text =
                "RES " + label + "\n"
                + line + "\n"
                + aux + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger\n"
                + "[ideal f0=1/(2pi sqrt(LC))]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 1f, 0.85f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
