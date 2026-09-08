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
    /// Grabbable MEASURE overshoot meter: while near an InductionCircuit, measures
    /// classical Emf overshoot % (OSV), I overshoot % (OSI), and peak |Emf| (PK)
    /// from sliding-window min/max and settled plateaus after edges. Honesty: ideal
    /// overshoot % from classical InductionCircuit Emf/I samples - NOT scope cursors,
    /// not a real overshoot meter, not BER/edge analyzer, not calibrated pulse-edge /
    /// step-response hardware. CLR zeros peaks/windows then snaps to OSV. XR activate /
    /// N cycles; P steps back. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadOvershootMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_OvershootMeter";
        public const string Honesty =
            "Ideal overshoot % from classical InductionCircuit Emf/I samples. NOT scope cursors, not real overshoot meter, not BER/edge analyzer, not calibrated pulse-edge / step-response hardware.";

        enum Mode { Osv, Osi, Pk, Clr }

        static readonly string[] PresetLabels = { "OSV", "OSI", "PK", "CLR" };
        static readonly Mode[] PresetModes = { Mode.Osv, Mode.Osi, Mode.Pk, Mode.Clr };

        const float Floor = 1e-12f;
        const float LoFrac = 0.15f;
        const float HiFrac = 0.85f;
        const float SettleBandFrac = 0.04f;
        const float SettleNeed = 0.08f;

        struct Chan
        {
            public float winMin;
            public float winMax;
            public float winAge;
            public int phase; // 0 idle, 1 rising, 2 settling-rise, 3 falling, 4 settling-fall
            public float startLevel;
            public float peak;
            public float trough;
            public float settleAge;
            public float lastOs;
            public float lastUs;
            public float livePeak;
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
        int _presetIndex; // default OSV
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        Chan _v;
        Chan _i;
        float _peakAbsEmf;
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

            SampleOvershoot();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        static void InitChan(ref Chan c)
        {
            c.winMin = float.PositiveInfinity;
            c.winMax = float.NegativeInfinity;
            c.winAge = 0f;
            c.phase = 0;
            c.startLevel = 0f;
            c.peak = float.NegativeInfinity;
            c.trough = float.PositiveInfinity;
            c.settleAge = 0f;
            c.lastOs = 0f;
            c.lastUs = 0f;
            c.livePeak = 0f;
        }

        void ResetPeaks()
        {
            InitChan(ref _v);
            InitChan(ref _i);
            _peakAbsEmf = 0f;
            _glow = 0f;
        }

        static void SoftShrinkWindow(ref Chan c)
        {
            if (c.winAge <= 0.75f)
                return;
            float mid = 0.5f * (c.winMin + c.winMax);
            float half = 0.5f * (c.winMax - c.winMin);
            c.winMin = mid - half * 0.85f;
            c.winMax = mid + half * 0.85f;
            c.winAge = 0f;
        }

        static float OvershootPct(float peak, float settled, float start)
        {
            float step = Mathf.Abs(settled - start);
            if (step < 1e-6f)
                return 0f;
            float os = peak - settled;
            if (os <= 0f)
                return 0f;
            return 100f * os / step;
        }

        static float UndershootPct(float trough, float settled, float start)
        {
            float step = Mathf.Abs(settled - start);
            if (step < 1e-6f)
                return 0f;
            float us = settled - trough;
            if (us <= 0f)
                return 0f;
            return 100f * us / step;
        }

        static void TickChan(ref Chan c, float x, float dt)
        {
            if (dt <= Floor)
                return;

            c.winAge += dt;
            if (x < c.winMin) c.winMin = x;
            if (x > c.winMax) c.winMax = x;
            SoftShrinkWindow(ref c);

            float span = c.winMax - c.winMin;
            if (span < 1e-4f)
            {
                c.phase = 0;
                c.settleAge = 0f;
                return;
            }

            float lo = c.winMin + LoFrac * span;
            float hi = c.winMin + HiFrac * span;
            float band = Mathf.Max(SettleBandFrac * span, 1e-5f);

            if (c.phase == 0)
            {
                if (x <= lo)
                {
                    c.phase = 1;
                    c.startLevel = x;
                    c.peak = x;
                    c.livePeak = x;
                    c.settleAge = 0f;
                }
                else if (x >= hi)
                {
                    c.phase = 3;
                    c.startLevel = x;
                    c.trough = x;
                    c.livePeak = x;
                    c.settleAge = 0f;
                }
            }
            else if (c.phase == 1)
            {
                if (x > c.peak) c.peak = x;
                c.livePeak = c.peak;
                if (x <= lo)
                {
                    c.startLevel = x;
                    c.peak = x;
                    c.settleAge = 0f;
                }
                else if (x >= hi)
                {
                    c.phase = 2;
                    c.settleAge = 0f;
                }
            }
            else if (c.phase == 2)
            {
                if (x > c.peak) c.peak = x;
                c.livePeak = c.peak;
                if (x < c.peak - band)
                {
                    c.settleAge += dt;
                    if (c.settleAge >= SettleNeed)
                    {
                        float settled = x;
                        float os = OvershootPct(c.peak, settled, c.startLevel);
                        if (os > 0f || c.lastOs <= Floor)
                            c.lastOs = os;
                        c.phase = 3;
                        c.startLevel = settled;
                        c.trough = settled;
                        c.settleAge = 0f;
                    }
                }
                else
                    c.settleAge = 0f;
            }
            else if (c.phase == 3)
            {
                if (x < c.trough) c.trough = x;
                c.livePeak = c.trough;
                if (x >= hi)
                {
                    c.startLevel = x;
                    c.trough = x;
                    c.settleAge = 0f;
                }
                else if (x <= lo)
                {
                    c.phase = 4;
                    c.settleAge = 0f;
                }
            }
            else if (c.phase == 4)
            {
                if (x < c.trough) c.trough = x;
                c.livePeak = c.trough;
                if (x > c.trough + band)
                {
                    c.settleAge += dt;
                    if (c.settleAge >= SettleNeed)
                    {
                        float settled = x;
                        float us = UndershootPct(c.trough, settled, c.startLevel);
                        if (us > 0f || c.lastUs <= Floor)
                            c.lastUs = us;
                        c.phase = 1;
                        c.startLevel = settled;
                        c.peak = settled;
                        c.settleAge = 0f;
                    }
                }
                else
                    c.settleAge = 0f;
            }
        }

        void SampleOvershoot()
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

            if (float.IsPositiveInfinity(_v.winMin))
                InitChan(ref _v);
            if (float.IsPositiveInfinity(_i.winMin))
                InitChan(ref _i);

            float emf = _circuit.EmfVolts;
            float cur = _circuit.CurrentAmperes;
            float a = Mathf.Abs(emf);
            if (a > _peakAbsEmf)
                _peakAbsEmf = a;

            TickChan(ref _v, emf, dt);
            TickChan(ref _i, cur, dt);

            float mag = Mathf.Max(_v.lastOs, _v.lastUs, _i.lastOs, _i.lastUs);
            _glow = Mathf.Clamp01(mag / 40f);
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
                ResetPeaks();
                for (int i = 0; i < PresetModes.Length; i++)
                {
                    if (PresetModes[i] == Mode.Osv)
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
                Color baseC = new Color(0.55f, 0.28f, 0.42f, 1f);
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
                Color faceC = new Color(0.12f, 0.04f, 0.08f, 1f);
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
            Color pulse = new Color(1f, 0.40f, 0.72f, 1f) * (0.08f + 1.2f * _glow);
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
            _readout.color = new Color(1f, 0.78f, 0.90f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "OSV\nseeking circuit...";
        }

        static string FormatPct(float p)
        {
            if (float.IsNaN(p) || p < 0f)
                return "---";
            if (p < 0.01f && p > 0f)
                return p.ToString("0.000") + "%";
            if (p < 10f)
                return p.ToString("0.00") + "%";
            return p.ToString("0.0") + "%";
        }

        static string FormatVolts(float v)
        {
            if (float.IsNaN(v) || v < 0f)
                return "---";
            float a = Mathf.Abs(v);
            if (a < 1e-3f)
                return (v * 1e6f).ToString("0.00") + " uV";
            if (a < 1f)
                return (v * 1e3f).ToString("0.00") + " mV";
            return v.ToString("0.000") + " V";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "OS " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
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
                case Mode.Osi:
                    line = "OSI " + FormatPct(_i.lastOs) + "\nI overshoot" + (_i.lastUs > 0.01f ? "  US " + FormatPct(_i.lastUs) : "");
                    break;
                case Mode.Pk:
                    line = "PK " + FormatVolts(_peakAbsEmf) + "\npeak |Emf|";
                    break;
                case Mode.Clr:
                    line = "CLR -> OSV";
                    break;
                default:
                    line = "OSV " + FormatPct(_v.lastOs) + "\nEmf overshoot" + (_v.lastUs > 0.01f ? "  US " + FormatPct(_v.lastUs) : "");
                    break;
            }

            _readout.text = "OS " + label + "\n" + line + "\n" + target + " [" + link + "]\n" + Honesty.Substring(0, Mathf.Min(48, Honesty.Length)) + "..";
        }
    }
}
