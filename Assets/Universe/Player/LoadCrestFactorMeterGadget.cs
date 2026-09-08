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
    /// Grabbable MEASURE crest factor meter: while near an InductionCircuit, shows classical
    /// peak/RMS ratios for Emf and I (CFV, CFI) plus Emf form factor (FF = RMS / mean|Emf|).
    /// Honesty: ideal windowed peak and RMS from classical InductionCircuit EmfVolts /
    /// CurrentAmperes - NOT true-RMS ADC, not IEC crest-factor meter, not THD, not scope math,
    /// not calibrated waveform analyzer. CLR zeros window then snaps to CFV.
    /// XR activate / N cycles; P steps back. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadCrestFactorMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_CrestFactorMeter";
        public const string Honesty =
            "Ideal windowed peak/RMS crest + form factor from classical InductionCircuit Emf / I. NOT true-RMS ADC, not IEC crest-factor meter, not THD, not scope math, not calibrated waveform analyzer.";

        enum Mode { Cfv, Cfi, Ff, Clr }

        static readonly string[] PresetLabels = { "CFV", "CFI", "FF", "CLR" };
        static readonly Mode[] PresetModes = { Mode.Cfv, Mode.Cfi, Mode.Ff, Mode.Clr };

        const float Floor = 1e-9f;

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
        int _presetIndex; // default CFV
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        float _peakAbsEmf;
        float _peakAbsI;
        float _sumSqEmf;
        float _sumSqI;
        float _sumAbsEmf;
        float _windowSeconds;
        float _liveCfv;
        float _liveCfi;
        float _liveFf;
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

            SampleWindow();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        void ResetWindow()
        {
            _peakAbsEmf = 0f;
            _peakAbsI = 0f;
            _sumSqEmf = 0f;
            _sumSqI = 0f;
            _sumAbsEmf = 0f;
            _windowSeconds = 0f;
            _liveCfv = 0f;
            _liveCfi = 0f;
            _liveFf = 0f;
        }

        void SampleWindow()
        {
            _linked = false;
            if (_circuit == null)
                return;

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            if (dist > connectWithinMeters)
                return;

            _linked = true;
            float dt = Time.timeScale <= 0f ? 0f : Time.unscaledDeltaTime;
            if (dt <= 0f)
                return;

            float emf = _circuit.EmfVolts;
            float i = _circuit.CurrentAmperes;
            float absEmf = Mathf.Abs(emf);
            float absI = Mathf.Abs(i);

            if (absEmf > _peakAbsEmf)
                _peakAbsEmf = absEmf;
            if (absI > _peakAbsI)
                _peakAbsI = absI;

            _sumSqEmf += emf * emf * dt;
            _sumSqI += i * i * dt;
            _sumAbsEmf += absEmf * dt;
            _windowSeconds += dt;

            float rmsEmf = _windowSeconds > Floor ? Mathf.Sqrt(_sumSqEmf / _windowSeconds) : 0f;
            float rmsI = _windowSeconds > Floor ? Mathf.Sqrt(_sumSqI / _windowSeconds) : 0f;
            float meanAbsEmf = _windowSeconds > Floor ? (_sumAbsEmf / _windowSeconds) : 0f;

            _liveCfv = rmsEmf > Floor ? (_peakAbsEmf / rmsEmf) : 0f;
            _liveCfi = rmsI > Floor ? (_peakAbsI / rmsI) : 0f;
            _liveFf = meanAbsEmf > Floor ? (rmsEmf / meanAbsEmf) : 0f;

            float cf = ActiveMode == Mode.Cfi ? _liveCfi : _liveCfv;
            // Map ~1..3 crest into glow (sine ~1.41).
            _glow = Mathf.Clamp01((cf - 1f) / 2f);
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
                ResetWindow();
                for (int i = 0; i < PresetModes.Length; i++)
                {
                    if (PresetModes[i] == Mode.Cfv)
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
                Color baseC = new Color(0.55f, 0.28f, 0.18f, 1f);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", baseC);
                _mat.color = baseC;
                if (_mat.HasProperty("_Metallic"))
                    _mat.SetFloat("_Metallic", 0.48f);
                if (_mat.HasProperty("_Smoothness"))
                    _mat.SetFloat("_Smoothness", 0.42f);
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
                Color faceC = new Color(0.12f, 0.06f, 0.04f, 1f);
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
            Color pulse = new Color(1f, 0.55f, 0.25f, 1f) * (0.08f + 1.2f * _glow);
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
            _readout.color = new Color(1f, 0.78f, 0.55f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "CF\nseeking circuit...";
        }

        static string FormatRatio(float v)
        {
            if (float.IsNaN(v) || v <= 0f)
                return "---";
            if (v > 99f)
                return ">99";
            return v.ToString("0.000");
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "CF " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON circuit" : (dist > connectWithinMeters ? "far" : "near");
            string line;
            switch (ActiveMode)
            {
                case Mode.Cfi:
                    line = "CFI " + FormatRatio(_liveCfi) + "\npeak|I|/Irms";
                    break;
                case Mode.Ff:
                    line = "FF " + FormatRatio(_liveFf) + "\nErms/mean|E|";
                    break;
                case Mode.Clr:
                    line = "CLR -> CFV";
                    break;
                default:
                    line = "CFV " + FormatRatio(_liveCfv) + "\npeak|E|/Erms";
                    break;
            }

            string win = _windowSeconds < Floor ? "win ---" : ("win " + _windowSeconds.ToString("0.00") + "s");
            _readout.text = "CF " + label + "\n" + line + "\n" + win + "\n" + target + " [" + link + "]\n" + Honesty.Substring(0, Mathf.Min(48, Honesty.Length)) + "..";
        }
    }
}
