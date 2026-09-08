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
    /// Grabbable MEASURE THD meter: while near an InductionCircuit, measures
    /// classical windowed harmonic residual THD for Emf (THDV), I (THDI), and
    /// sine-assumed fundamental amplitude (FUND). THD=sqrt(max(0,Vrms^2-Vfund^2))/max(|Vfund|,eps)
    /// with Vfund from mean|x|*pi/(2*sqrt(2)). Honesty: ideal windowed harmonic
    /// residual from classical InductionCircuit Emf/I samples - NOT real THD
    /// analyzer, not FFT spectrum analyzer, not IEC distortion meter, not
    /// calibrated audio THD hardware. CLR zeros window then snaps to THDV.
    /// XR activate / N cycles; P steps back. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadThdMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_ThdMeter";
        public const string Honesty =
            "Ideal windowed harmonic residual from classical InductionCircuit Emf/I samples. NOT real THD analyzer, not FFT spectrum analyzer, not IEC distortion meter, not calibrated audio THD hardware.";

        enum Mode { Thdv, Thdi, Fund, Clr }

        static readonly string[] PresetLabels = { "THDV", "THDI", "FUND", "CLR" };
        static readonly Mode[] PresetModes = { Mode.Thdv, Mode.Thdi, Mode.Fund, Mode.Clr };

        // Sine form-factor: RMS = mean(|x|) * pi / (2*sqrt(2))
        const float SineMavToRms = 1.1107207345f;
        const float Floor = 1e-12f;

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
        int _presetIndex; // default THDV
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        float _sumEmf2;
        float _sumI2;
        float _sumAbsEmf;
        float _sumAbsI;
        float _windowSeconds;
        float _liveVrms;
        float _liveIrms;
        float _liveVfund;
        float _liveIfund;
        float _liveThdv;
        float _liveThdi;
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

            SampleThd();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        void ResetAccumulators()
        {
            _sumEmf2 = 0f;
            _sumI2 = 0f;
            _sumAbsEmf = 0f;
            _sumAbsI = 0f;
            _windowSeconds = 0f;
            _liveVrms = 0f;
            _liveIrms = 0f;
            _liveVfund = 0f;
            _liveIfund = 0f;
            _liveThdv = 0f;
            _liveThdi = 0f;
            _glow = 0f;
        }

        void SampleThd()
        {
            _linked = false;
            if (_circuit == null)
                return;

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            if (dist > connectWithinMeters)
                return;

            _linked = true;
            float dt = Time.timeScale <= 0f ? 0f : Time.unscaledDeltaTime;
            if (dt <= Floor)
                return;

            float emf = _circuit.EmfVolts;
            float cur = _circuit.CurrentAmperes;
            _sumEmf2 += emf * emf * dt;
            _sumI2 += cur * cur * dt;
            _sumAbsEmf += Mathf.Abs(emf) * dt;
            _sumAbsI += Mathf.Abs(cur) * dt;
            _windowSeconds += dt;

            if (_windowSeconds > Floor)
            {
                float vrms2 = _sumEmf2 / _windowSeconds;
                float irms2 = _sumI2 / _windowSeconds;
                _liveVrms = Mathf.Sqrt(Mathf.Max(0f, vrms2));
                _liveIrms = Mathf.Sqrt(Mathf.Max(0f, irms2));
                float mavEmf = _sumAbsEmf / _windowSeconds;
                float mavI = _sumAbsI / _windowSeconds;
                // Sine-assumed fundamental RMS from mean-absolute (Mean/Crest classical style).
                _liveVfund = mavEmf * SineMavToRms;
                _liveIfund = mavI * SineMavToRms;
                float vFund = Mathf.Max(_liveVfund, Floor);
                float iFund = Mathf.Max(_liveIfund, Floor);
                float thdvNum2 = Mathf.Max(0f, vrms2 - _liveVfund * _liveVfund);
                float thdiNum2 = Mathf.Max(0f, irms2 - _liveIfund * _liveIfund);
                _liveThdv = Mathf.Sqrt(thdvNum2) / vFund;
                _liveThdi = Mathf.Sqrt(thdiNum2) / iFund;
            }
            else
            {
                _liveVrms = 0f;
                _liveIrms = 0f;
                _liveVfund = 0f;
                _liveIfund = 0f;
                _liveThdv = 0f;
                _liveThdi = 0f;
            }

            _glow = Mathf.Clamp01(Mathf.Max(_liveThdv, _liveThdi, _liveVfund * 0.5f) / 2f);
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
                ResetAccumulators();
                for (int i = 0; i < PresetModes.Length; i++)
                {
                    if (PresetModes[i] == Mode.Thdv)
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
                Color baseC = new Color(0.42f, 0.28f, 0.58f, 1f);
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
                Color faceC = new Color(0.10f, 0.06f, 0.14f, 1f);
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
            Color pulse = new Color(0.78f, 0.45f, 0.95f, 1f) * (0.08f + 1.2f * _glow);
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
            _readout.color = new Color(0.92f, 0.82f, 0.98f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "THDV\nseeking circuit...";
        }

        static string FormatRatio(float r)
        {
            if (float.IsNaN(r) || r < 0f)
                return "---";
            if (r < 1e-6f)
                return "0 %";
            if (r < 0.01f)
                return (r * 100f).ToString("0.000") + " %";
            if (r < 10f)
                return (r * 100f).ToString("0.00") + " %";
            return r.ToString("0.000") + " x";
        }

        static string FormatAbsVolts(float v)
        {
            if (float.IsNaN(v) || v < 0f)
                return "---";
            float a = Mathf.Abs(v);
            if (a < 1e-6f)
                return "0 V";
            if (a < 1e-3f)
                return (a * 1e6f).ToString("0.00") + " uV";
            if (a < 1f)
                return (a * 1e3f).ToString("0.00") + " mV";
            return a.ToString("0.000") + " V";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "THD " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON circuit" : (dist > connectWithinMeters ? "far" : "near");
            string win = "t=" + _windowSeconds.ToString("0.0") + "s";
            string line;
            switch (ActiveMode)
            {
                case Mode.Thdi:
                    line = "THDI " + FormatRatio(_liveThdi) + "\nI harm/|Ifund|  " + win;
                    break;
                case Mode.Fund:
                    line = "FUND " + FormatAbsVolts(_liveVfund) + "\nEmf fund  " + win;
                    break;
                case Mode.Clr:
                    line = "CLR -> THDV";
                    break;
                default:
                    line = "THDV " + FormatRatio(_liveThdv) + "\nV harm/|Vfund|  " + win;
                    break;
            }

            _readout.text = "THD " + label + "\n" + line + "\n" + target + " [" + link + "]\n" + Honesty.Substring(0, Mathf.Min(48, Honesty.Length)) + "..";
        }
    }
}
