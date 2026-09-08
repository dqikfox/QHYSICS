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
    /// Grabbable MEASURE admittance meter: while near an InductionCircuit, shows classical
    /// series RLC |Y|=1/|Z|, conductance G=R/|Z|^2, susceptance B=-X/|Z|^2, or phase PHI.
    /// Honesty: ideal series RLC admittance from classical InductionCircuit R / Series L / Series C
    /// at estimated drive omega (rising Emf zero-cross gate) or omega0=1/sqrt(LC) fallback —
    /// NOT real admittance bridge, not network analyzer, not VNA Y-params, not FFT complex Y.
    /// CLR zeros peak |Y|, resets energy storage (Vc + I_L), snaps to Y.
    /// XR activate / N cycles preset; P steps backward. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadAdmittanceMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_AdmittanceMeter";
        public const string Honesty =
            "Ideal series RLC admittance Y=1/Z from classical InductionCircuit R / Series L / Series C at estimated omega (or omega0 fallback). NOT real admittance bridge, not network analyzer, not VNA Y-params, not FFT complex Y.";

        enum Mode { Y, G, B, Phi, Clr }

        static readonly string[] PresetLabels = { "Y", "G", "B", "PHI", "CLR" };
        static readonly Mode[] PresetModes = { Mode.Y, Mode.G, Mode.B, Mode.Phi, Mode.Clr };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay linked and sample.")]
        float connectWithinMeters = 0.85f;

        [SerializeField, Tooltip("Rising zero-cross gate window for drive frequency estimate (seconds).")]
        float freqGateSeconds = 0.75f;

        [SerializeField, Tooltip("Deadband around zero so noise does not fake Emf crossings (volts).")]
        float zeroDeadband = 1e-5f;

        TextMeshPro _readout;
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex; // default Y
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        float _liveR;
        float _liveL;
        float _liveC;
        float _liveOmega;
        float _liveX;
        float _liveZmag;
        float _liveYmag;
        float _liveG;
        float _liveB;
        float _livePhiDeg;
        bool _yValid;
        float _peakAbsY;
        bool _omegaFromGate;
        bool _omegaFromResonance;
        float _glow;

        float _prevEmf;
        bool _havePrevEmf;
        int _crossings;
        float _gateStart;
        float _gateHz;
        float _periodHz;
        float _lastCrossTime;

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
            ResetFreqGate();
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

            SampleAdmittance();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        void SampleAdmittance()
        {
            _linked = false;
            _liveR = 0f;
            _liveL = 0f;
            _liveC = 0f;
            _liveOmega = 0f;
            _liveX = 0f;
            _liveZmag = 0f;
            _liveYmag = 0f;
            _liveG = 0f;
            _liveB = 0f;
            _livePhiDeg = 0f;
            _yValid = false;
            _omegaFromGate = false;
            _omegaFromResonance = false;

            if (_circuit == null)
            {
                _havePrevEmf = false;
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            if (dist > connectWithinMeters)
            {
                _havePrevEmf = false;
                return;
            }

            _linked = true;
            _liveR = _circuit.TotalResistanceOhms;
            var coil = _circuit.Coil;
            _liveL = coil != null ? coil.SeriesInductance : 0f;
            _liveC = coil != null ? coil.SeriesCapacitance : 0f;

            UpdateFreqGate(_circuit.EmfVolts);

            float hz = 0f;
            if (_periodHz > 0.05f)
                hz = _periodHz;
            else if (_gateHz > 0.05f)
                hz = _gateHz;

            if (hz > 0.05f)
            {
                _liveOmega = 2f * Mathf.PI * hz;
                _omegaFromGate = true;
            }
            else if (_liveL > 0f && _liveC > 0f)
            {
                _liveOmega = 1f / Mathf.Sqrt(_liveL * _liveC);
                _omegaFromResonance = true;
            }

            if (_liveOmega > 0f)
            {
                float xL = _liveL > 0f ? _liveOmega * _liveL : 0f;
                float xC = 0f;
                if (_liveC > 0f)
                    xC = 1f / (_liveOmega * _liveC);

                if (_liveL > 0f && _liveC > 0f)
                    _liveX = xL - xC;
                else if (_liveL > 0f)
                    _liveX = xL;
                else if (_liveC > 0f)
                    _liveX = -xC;
                else
                    _liveX = 0f;
            }
            else
            {
                _liveX = 0f;
            }

            _liveZmag = Mathf.Sqrt(_liveR * _liveR + _liveX * _liveX);
            if (_liveZmag > 1e-9f)
            {
                _liveYmag = 1f / _liveZmag;
                _liveG = _liveR / (_liveZmag * _liveZmag);
                _liveB = -_liveX / (_liveZmag * _liveZmag);
                _livePhiDeg = Mathf.Atan2(_liveB, _liveG) * Mathf.Rad2Deg;
                _yValid = true;
                if (_liveYmag > _peakAbsY)
                    _peakAbsY = _liveYmag;
            }

            _glow = Mathf.Clamp01(_yValid ? _liveYmag * 40f : 0f);
        }

        void UpdateFreqGate(float emf)
        {
            float dead = Mathf.Max(1e-9f, zeroDeadband);
            if (_havePrevEmf)
            {
                bool rose =
                    (_prevEmf <= -dead && emf >= dead)
                    || (_prevEmf < 0f && emf >= 0f && (emf - _prevEmf) > dead);
                if (rose)
                {
                    _crossings++;
                    float now = Time.time;
                    if (_lastCrossTime > 0f)
                    {
                        float period = now - _lastCrossTime;
                        if (period > 1e-4f && period < 10f)
                            _periodHz = 1f / period;
                    }
                    _lastCrossTime = now;
                }
            }
            _prevEmf = emf;
            _havePrevEmf = true;

            float gate = Mathf.Clamp(freqGateSeconds, 0.5f, 1f);
            float elapsed = Time.time - _gateStart;
            if (_gateStart <= 0f)
            {
                _gateStart = Time.time;
                return;
            }
            if (elapsed >= gate)
            {
                _gateHz = _crossings / Mathf.Max(1e-4f, elapsed);
                _crossings = 0;
                _gateStart = Time.time;
            }
        }

        void ResetFreqGate()
        {
            _crossings = 0;
            _gateStart = Time.time;
            _gateHz = 0f;
            _periodHz = 0f;
            _lastCrossTime = 0f;
            _havePrevEmf = false;
            _prevEmf = 0f;
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
                _peakAbsY = 0f;
                if (_circuit != null)
                    _circuit.ResetEnergyStorage();
                ResetFreqGate();
                for (int i = 0; i < PresetModes.Length; i++)
                {
                    if (PresetModes[i] == Mode.Y)
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
                Color baseC = new Color(0.18f, 0.55f, 0.62f, 1f);
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
                Color faceC = new Color(0.04f, 0.12f, 0.14f, 1f);
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
            Color pulse = new Color(0.35f, 0.95f, 1f, 1f) * (0.08f + 1.2f * _glow);
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
            _readout.color = new Color(0.45f, 0.95f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "Y\nseeking circuit...";
        }

        static string FormatSiemens(float s)
        {
            float a = Mathf.Abs(s);
            if (a < 1e-9f)
                return "0.00nS";
            if (a < 1e-6f)
                return (s * 1e9f).ToString("0.##") + "nS";
            if (a < 1e-3f)
                return (s * 1e6f).ToString("0.###") + "uS";
            if (a < 1f)
                return (s * 1000f).ToString("0.###") + "mS";
            return s.ToString("0.###") + "S";
        }

        static string FormatOhms(float ohms)
        {
            float r = Mathf.Abs(ohms);
            if (r < 1e-3f)
                return "0.00mohm";
            if (r < 1f)
                return (ohms * 1000f).ToString("0.0") + "mohm";
            if (r < 1000f)
                return ohms.ToString("0.###") + "ohm";
            if (r < 1e6f)
                return (ohms / 1000f).ToString("0.###") + "kohm";
            return (ohms / 1e6f).ToString("0.###") + "Mohm";
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

        string OmegaHonestyTag()
        {
            if (_omegaFromGate)
                return "[w from Emf gate]";
            if (_omegaFromResonance)
                return "[w0=1/sqrt(LC) fallback]";
            return "[no w yet]";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "Y " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON circuit" : (dist > connectWithinMeters ? "far" : "near");
            bool hasOmega = _liveOmega > 0f;
            string line;
            switch (ActiveMode)
            {
                case Mode.G:
                    line = _yValid ? ("G " + FormatSiemens(_liveG)) : "G ---";
                    break;
                case Mode.B:
                    line = _yValid ? ("B " + FormatSiemens(_liveB)) : "B ---";
                    break;
                case Mode.Phi:
                    line = _yValid
                        ? ("phi " + _livePhiDeg.ToString("0.#") + "deg")
                        : "phi ---";
                    break;
                case Mode.Clr:
                case Mode.Y:
                default:
                    line = _yValid ? ("|Y| " + FormatSiemens(_liveYmag)) : "|Y| ---";
                    break;
            }

            string aux;
            if (ActiveMode == Mode.G)
                aux = "B " + FormatSiemens(_liveB) + " |Z| " + FormatOhms(_liveZmag);
            else if (ActiveMode == Mode.B)
                aux = "G " + FormatSiemens(_liveG) + " |Z| " + FormatOhms(_liveZmag);
            else if (ActiveMode == Mode.Phi)
                aux = "G " + FormatSiemens(_liveG) + " B " + FormatSiemens(_liveB);
            else
                aux = "pk|Y| " + FormatSiemens(_peakAbsY) + " |Z| " + FormatOhms(_liveZmag);

            float hzShow = _liveOmega > 0f ? _liveOmega / (2f * Mathf.PI) : 0f;
            string fLine = hasOmega ? ("f " + FormatHz(hzShow)) : "f ---";

            _readout.text =
                "Y " + label + "\n"
                + line + "\n"
                + aux + "\n"
                + fLine + " " + OmegaHonestyTag() + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger\n"
                + "[ideal series RLC Y=1/Z]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.95f, 1f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
