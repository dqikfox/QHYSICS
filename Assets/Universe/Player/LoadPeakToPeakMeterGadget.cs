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
    /// Grabbable MEASURE peak-to-peak meter: while near an InductionCircuit, measures
    /// classical Emf Vpp (PPV), I Ipp (PPI), and live Emf window span (LIVE) from a
    /// soft-shrinking min/max window. Honesty: ideal peak-to-peak from classical
    /// InductionCircuit Emf/I samples - NOT scope cursors, not a real Vpp meter,
    /// not true-RMS ADC, not calibrated waveform analyzer. CLR zeros windows then
    /// snaps to PPV. XR activate / N cycles; P steps back. Desktop LMB/scroll via
    /// IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadPeakToPeakMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_PeakToPeakMeter";
        public const string Honesty =
            "Ideal peak-to-peak from classical InductionCircuit Emf/I samples. NOT scope cursors, not real Vpp meter, not true-RMS ADC, not calibrated waveform analyzer.";

        enum Mode { Ppv, Ppi, Live, Clr }

        static readonly string[] PresetLabels = { "PPV", "PPI", "LIVE", "CLR" };
        static readonly Mode[] PresetModes = { Mode.Ppv, Mode.Ppi, Mode.Live, Mode.Clr };

        const float Floor = 1e-12f;

        struct Win
        {
            public float min;
            public float max;
            public float age;
        }

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
        int _presetIndex; // default PPV
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        Win _v;
        Win _i;
        float _heldPpv;
        float _heldPpi;
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

            SamplePeakToPeak();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        static void InitWin(ref Win w)
        {
            w.min = float.PositiveInfinity;
            w.max = float.NegativeInfinity;
            w.age = 0f;
        }

        void ResetWindows()
        {
            InitWin(ref _v);
            InitWin(ref _i);
            _heldPpv = 0f;
            _heldPpi = 0f;
            _glow = 0f;
        }

        static void SoftShrink(ref Win w)
        {
            if (w.age <= 0.85f)
                return;
            float mid = 0.5f * (w.min + w.max);
            float half = 0.5f * (w.max - w.min);
            w.min = mid - half * 0.88f;
            w.max = mid + half * 0.88f;
            w.age = 0f;
        }

        static float Span(in Win w)
        {
            if (float.IsPositiveInfinity(w.min) || float.IsNegativeInfinity(w.max))
                return 0f;
            float s = w.max - w.min;
            return s > 0f ? s : 0f;
        }

        static void TickWin(ref Win w, float x, float dt)
        {
            if (dt <= Floor)
                return;
            if (float.IsPositiveInfinity(w.min))
                InitWin(ref w);
            w.age += dt;
            if (x < w.min) w.min = x;
            if (x > w.max) w.max = x;
            SoftShrink(ref w);
        }

        void SamplePeakToPeak()
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
            TickWin(ref _v, emf, dt);
            TickWin(ref _i, cur, dt);

            float ppv = Span(_v);
            float ppi = Span(_i);
            if (ppv > _heldPpv) _heldPpv = ppv;
            if (ppi > _heldPpi) _heldPpi = ppi;

            _glow = Mathf.Clamp01(Mathf.Max(ppv, ppi * 10f) / 2f);
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
                ResetWindows();
                for (int i = 0; i < PresetModes.Length; i++)
                {
                    if (PresetModes[i] == Mode.Ppv)
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
                Color baseC = new Color(0.42f, 0.32f, 0.58f, 1f);
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
                Color faceC = new Color(0.08f, 0.05f, 0.14f, 1f);
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
            Color pulse = new Color(0.72f, 0.48f, 1f, 1f) * (0.08f + 1.2f * _glow);
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
            _readout.color = new Color(0.90f, 0.82f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "PPV\nseeking circuit...";
        }

        static string FormatVolts(float v)
        {
            if (float.IsNaN(v) || v < 0f)
                return "---";
            float a = Mathf.Abs(v);
            if (a < 1e-6f)
                return "0 V";
            if (a < 1e-3f)
                return (v * 1e6f).ToString("0.00") + " uV";
            if (a < 1f)
                return (v * 1e3f).ToString("0.00") + " mV";
            return v.ToString("0.000") + " V";
        }

        static string FormatAmps(float a)
        {
            if (float.IsNaN(a) || a < 0f)
                return "---";
            float x = Mathf.Abs(a);
            if (x < 1e-6f)
                return "0 A";
            if (x < 1e-3f)
                return (a * 1e6f).ToString("0.00") + " uA";
            if (x < 1f)
                return (a * 1e3f).ToString("0.00") + " mA";
            return a.ToString("0.000") + " A";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "PP " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON circuit" : (dist > connectWithinMeters ? "far" : "near");
            float livePpv = Span(_v);
            float livePpi = Span(_i);
            string line;
            switch (ActiveMode)
            {
                case Mode.Ppi:
                    line = "PPI " + FormatAmps(_heldPpi) + "\nI peak-peak  live " + FormatAmps(livePpi);
                    break;
                case Mode.Live:
                    line = "LIVE " + FormatVolts(livePpv) + "\nEmf window span";
                    break;
                case Mode.Clr:
                    line = "CLR -> PPV";
                    break;
                default:
                    line = "PPV " + FormatVolts(_heldPpv) + "\nEmf peak-peak  live " + FormatVolts(livePpv);
                    break;
            }

            _readout.text = "PP " + label + "\n" + line + "\n" + target + " [" + link + "]\n" + Honesty.Substring(0, Mathf.Min(48, Honesty.Length)) + "..";
        }
    }
}
