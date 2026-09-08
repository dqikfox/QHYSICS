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
    /// Grabbable MEASURE rise/fall meter: while near an InductionCircuit, measures
    /// classical 10%-90% Emf rise time (RISE), 90%-10% fall time (FALL), and live
    /// in-progress edge duration (LIVE). Honesty: ideal threshold timing from
    /// InductionCircuit Emf samples - NOT a real rise-time meter, not scope cursors,
    /// not BER/edge analyzer, not calibrated pulse-edge hardware. CLR zeros times
    /// then snaps to RISE. XR activate / N cycles; P steps back. Desktop LMB/scroll
    /// via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadRiseFallMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_RiseFallMeter";
        public const string Honesty =
            "Ideal 10-90% Emf edge times from classical InductionCircuit samples. NOT rise-time meter, not scope cursors, not BER/edge analyzer, not calibrated pulse-edge hardware.";

        enum Mode { Rise, Fall, Live, Clr }

        static readonly string[] PresetLabels = { "RISE", "FALL", "LIVE", "CLR" };
        static readonly Mode[] PresetModes = { Mode.Rise, Mode.Fall, Mode.Live, Mode.Clr };

        const float Floor = 1e-12f;
        const float LoFrac = 0.1f;
        const float HiFrac = 0.9f;

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
        int _presetIndex; // default RISE
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        float _winMin = float.PositiveInfinity;
        float _winMax = float.NegativeInfinity;
        float _winAge;
        float _lastRise;
        float _lastFall;
        float _liveEdge;
        float _edgeStart;
        int _edgePhase; // 0 idle, 1 rising after lo, 2 falling after hi
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

            SampleEdges();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        void ResetTimes()
        {
            _winMin = float.PositiveInfinity;
            _winMax = float.NegativeInfinity;
            _winAge = 0f;
            _lastRise = 0f;
            _lastFall = 0f;
            _liveEdge = 0f;
            _edgeStart = 0f;
            _edgePhase = 0;
            _glow = 0f;
        }

        void SampleEdges()
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
            _winAge += dt;
            if (emf < _winMin) _winMin = emf;
            if (emf > _winMax) _winMax = emf;
            if (_winAge > 0.75f)
            {
                float mid = 0.5f * (_winMin + _winMax);
                float half = 0.5f * (_winMax - _winMin);
                _winMin = mid - half * 0.85f;
                _winMax = mid + half * 0.85f;
                _winAge = 0f;
            }

            float span = _winMax - _winMin;
            if (span < 1e-4f)
            {
                _liveEdge = 0f;
                _edgePhase = 0;
                return;
            }

            float lo = _winMin + LoFrac * span;
            float hi = _winMin + HiFrac * span;
            float now = Time.unscaledTime;

            if (_edgePhase == 0)
            {
                if (emf <= lo)
                    _edgePhase = 1;
                else if (emf >= hi)
                    _edgePhase = 2;
            }
            else if (_edgePhase == 1)
            {
                if (emf <= lo)
                {
                    _edgeStart = now;
                    _liveEdge = 0f;
                }
                else if (_edgeStart > Floor)
                {
                    _liveEdge = now - _edgeStart;
                    if (emf >= hi)
                    {
                        float t = now - _edgeStart;
                        if (t > 1e-5f && t < 2f)
                            _lastRise = t;
                        _edgePhase = 2;
                        _edgeStart = 0f;
                        _liveEdge = _lastRise;
                    }
                }
            }
            else if (_edgePhase == 2)
            {
                if (emf >= hi)
                {
                    _edgeStart = now;
                    _liveEdge = 0f;
                }
                else if (_edgeStart > Floor)
                {
                    _liveEdge = now - _edgeStart;
                    if (emf <= lo)
                    {
                        float t = now - _edgeStart;
                        if (t > 1e-5f && t < 2f)
                            _lastFall = t;
                        _edgePhase = 1;
                        _edgeStart = 0f;
                        _liveEdge = _lastFall;
                    }
                }
            }

            float refT = Mathf.Max(_lastRise, _lastFall, 1e-4f);
            _glow = Mathf.Clamp01(_liveEdge / refT);
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
                ResetTimes();
                for (int i = 0; i < PresetModes.Length; i++)
                {
                    if (PresetModes[i] == Mode.Rise)
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
                Color baseC = new Color(0.48f, 0.22f, 0.55f, 1f);
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
                Color faceC = new Color(0.10f, 0.04f, 0.12f, 1f);
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
            Color pulse = new Color(0.95f, 0.45f, 1f, 1f) * (0.08f + 1.2f * _glow);
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
            _readout.color = new Color(0.95f, 0.80f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "RISE\nseeking circuit...";
        }

        static string FormatTime(float t)
        {
            if (float.IsNaN(t) || t <= 0f)
                return "---";
            if (t < 1e-6f)
                return (t * 1e9f).ToString("0.00") + " ns";
            if (t < 1e-3f)
                return (t * 1e6f).ToString("0.00") + " us";
            if (t < 1f)
                return (t * 1e3f).ToString("0.00") + " ms";
            return t.ToString("0.000") + " s";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "RF " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
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
                case Mode.Fall:
                    line = "FALL " + FormatTime(_lastFall) + "\n90-10% Emf";
                    break;
                case Mode.Live:
                    line = "LIVE " + FormatTime(_liveEdge) + "\nedge in progress";
                    break;
                case Mode.Clr:
                    line = "CLR -> RISE";
                    break;
                default:
                    line = "RISE " + FormatTime(_lastRise) + "\n10-90% Emf";
                    break;
            }

            _readout.text = "RF " + label + "\n" + line + "\n" + target + " [" + link + "]\n" + Honesty.Substring(0, Mathf.Min(48, Honesty.Length)) + "..";
        }
    }
}
