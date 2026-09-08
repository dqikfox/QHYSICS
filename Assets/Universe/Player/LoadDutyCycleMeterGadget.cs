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
    /// Grabbable MEASURE duty-cycle meter: while near an InductionCircuit, measures
    /// classical windowed positive duty for Emf (DUTY) and I (DUTYI), plus period T
    /// from rising Emf zero-crosses.
    /// Honesty: ideal sample high-fraction and zero-cross gate from InductionCircuit -
    /// NOT a real duty-cycle meter, not oscilloscope duty, not PWM analyzer, not
    /// calibrated pulse-width / period counter hardware. CLR zeros the window then
    /// snaps to DUTY.
    /// XR activate / N cycles; P steps back. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadDutyCycleMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_DutyCycleMeter";
        public const string Honesty =
            "Ideal windowed positive duty + Emf zero-cross period from classical InductionCircuit Emf/I. NOT duty-cycle meter, not scope duty, not PWM analyzer, not calibrated pulse-width hardware.";

        enum Mode { Duty, DutyI, Period, Clr }

        static readonly string[] PresetLabels = { "DUTY", "DUTYI", "T", "CLR" };
        static readonly Mode[] PresetModes = { Mode.Duty, Mode.DutyI, Mode.Period, Mode.Clr };

        const float Floor = 1e-12f;

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay linked and sample.")]
        float connectWithinMeters = 0.85f;

        [SerializeField, Tooltip("Bipolar high threshold as fraction of peak |signal| (0 = sign-only).")]
        float highFractionOfPeak = 0f;

        TextMeshPro _readout;
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex; // default DUTY
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        float _windowSeconds;
        float _highEmfSeconds;
        float _highISeconds;
        float _peakAbsEmf;
        float _peakAbsI;
        float _liveDuty;
        float _liveDutyI;
        float _livePeriod;
        float _lastCrossTime = -1f;
        float _prevEmf;
        bool _havePrevEmf;
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

            SampleDuty();

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
            _windowSeconds = 0f;
            _highEmfSeconds = 0f;
            _highISeconds = 0f;
            _peakAbsEmf = 0f;
            _peakAbsI = 0f;
            _liveDuty = 0f;
            _liveDutyI = 0f;
            _livePeriod = 0f;
            _lastCrossTime = -1f;
            _havePrevEmf = false;
            _prevEmf = 0f;
            _glow = 0f;
        }

        void SampleDuty()
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

            float thrEmf = highFractionOfPeak * _peakAbsEmf;
            float thrI = highFractionOfPeak * _peakAbsI;
            // Classical positive duty: fraction of window where signal is above threshold
            // (default threshold 0 => sign-only / positive half for bipolar waveforms).
            if (emf > thrEmf)
                _highEmfSeconds += dt;
            if (i > thrI)
                _highISeconds += dt;

            _windowSeconds += dt;
            _liveDuty = _windowSeconds > Floor ? (_highEmfSeconds / _windowSeconds) : 0f;
            _liveDutyI = _windowSeconds > Floor ? (_highISeconds / _windowSeconds) : 0f;

            // Rising Emf zero-cross period gate (FrequencyCounter-style).
            if (_havePrevEmf && _prevEmf <= 0f && emf > 0f)
            {
                float now = Time.unscaledTime;
                if (_lastCrossTime > 0f)
                {
                    float period = now - _lastCrossTime;
                    if (period > 1e-4f && period < 10f)
                        _livePeriod = period;
                }
                _lastCrossTime = now;
            }
            _prevEmf = emf;
            _havePrevEmf = true;

            // Glow follows how far DUTY is from 50% (square-ish pulse emphasis).
            float d = Mathf.Abs(_liveDuty - 0.5f);
            _glow = Mathf.Clamp01(d * 2f);

            // Soft window decay so long holds don't freeze forever: roll every ~8s.
            if (_windowSeconds > 8f)
            {
                float keep = 0.5f;
                _windowSeconds *= keep;
                _highEmfSeconds *= keep;
                _highISeconds *= keep;
            }
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
                    if (PresetModes[i] == Mode.Duty)
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
                Color baseC = new Color(0.62f, 0.48f, 0.16f, 1f);
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
                Color faceC = new Color(0.14f, 0.10f, 0.03f, 1f);
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
            Color pulse = new Color(1f, 0.82f, 0.28f, 1f) * (0.08f + 1.2f * _glow);
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
            _readout.color = new Color(1f, 0.9f, 0.55f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "DUTY\nseeking circuit...";
        }

        static string FormatDuty(float d)
        {
            if (float.IsNaN(d))
                return "---";
            float pct = Mathf.Clamp01(d) * 100f;
            return pct.ToString("0.0") + "%";
        }

        static string FormatPeriod(float t)
        {
            if (float.IsNaN(t) || t <= Floor)
                return "---";
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
                _readout.text = "DUTY " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
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
                case Mode.DutyI:
                    line = "DUTYI " + FormatDuty(_liveDutyI) + "\nI > thr";
                    break;
                case Mode.Period:
                    line = "T " + FormatPeriod(_livePeriod) + "\nEmf rising ZX";
                    break;
                case Mode.Clr:
                    line = "CLR -> DUTY";
                    break;
                default:
                    line = "DUTY " + FormatDuty(_liveDuty) + "\nEmf > thr";
                    break;
            }

            string win = _windowSeconds < Floor ? "win ---" : ("win " + _windowSeconds.ToString("0.00") + "s");
            float hz = _livePeriod > Floor ? (1f / _livePeriod) : 0f;
            string hzLine = hz > Floor ? ("f " + hz.ToString("0.00") + "Hz") : "f ---";
            _readout.text = "DUTY " + label + "\n" + line + "\n" + win + " " + hzLine + "\n" + target + " [" + link + "]\n" + Honesty.Substring(0, Mathf.Min(48, Honesty.Length)) + "..";
        }
    }
}
