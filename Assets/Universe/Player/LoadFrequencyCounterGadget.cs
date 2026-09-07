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
    /// Grabbable MEASURE frequency counter: while near an InductionCircuit, estimates Hz from
    /// rising zero-crossings of classical Emf or I over a gate window.
    /// Honesty: ideal zero-cross gate from InductionCircuit samples — NOT a real counter/timer,
    /// not PLL, not FFT, not Schmitt trigger hysteresis, not probe capacitance.
    /// Does not modify the circuit. Pair with CIRCUIT Function Generator for EXPERIMENT loop.
    /// XR activate / N cycles preset; P steps backward. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadFrequencyCounterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_FrequencyCounter";
        public const string Honesty =
            "Ideal rising zero-cross gate from classical InductionCircuit Emf/I. NOT real counter/timer, not PLL, not FFT, not Schmitt hysteresis.";

        enum Channel { Emf, Current }

        static readonly string[] PresetLabels =
        {
            "EMF0.5", "EMF1", "EMF2",
            "I0.5", "I1", "I2"
        };
        static readonly Channel[] PresetChannels =
        {
            Channel.Emf, Channel.Emf, Channel.Emf,
            Channel.Current, Channel.Current, Channel.Current
        };
        static readonly float[] PresetGateSeconds =
        {
            0.5f, 1f, 2f,
            0.5f, 1f, 2f
        };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay linked and sample.")]
        float connectWithinMeters = 0.85f;

        [SerializeField, Tooltip("Deadband around zero so noise does not fake crossings (volts or amps).")]
        float zeroDeadband = 1e-5f;

        TextMeshPro _readout;
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex = 1; // default EMF1
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        float _prevSample;
        bool _havePrev;
        int _crossings;
        float _gateStart;
        float _gateHz;
        float _periodHz;
        float _lastCrossTime;
        float _liveSample;
        float _pulseFlash;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];
        public float ActiveGateSeconds => PresetGateSeconds[Mathf.Clamp(_presetIndex, 0, PresetGateSeconds.Length - 1)];
        Channel ActiveChannel => PresetChannels[Mathf.Clamp(_presetIndex, 0, PresetChannels.Length - 1)];

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
            ResetGate();
            RefreshText();
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void OnEnable()
        {
            WireGrab();
            ResetGate();
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

            SampleAndCount();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        void SampleAndCount()
        {
            _linked = false;
            _liveSample = 0f;
            if (_circuit == null)
            {
                _havePrev = false;
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            if (dist > connectWithinMeters)
            {
                _havePrev = false;
                return;
            }

            _linked = true;
            float sample = ActiveChannel == Channel.Current
                ? _circuit.CurrentAmperes
                : _circuit.EmfVolts;
            _liveSample = sample;

            float dead = Mathf.Max(1e-9f, zeroDeadband);
            if (_havePrev)
            {
                // Rising zero-cross: previous <= -dead and current >= +dead (or through 0 with signs).
                bool rose =
                    (_prevSample <= -dead && sample >= dead)
                    || (_prevSample < 0f && sample >= 0f && (sample - _prevSample) > dead);
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
                    _pulseFlash = 1f;
                }
            }
            _prevSample = sample;
            _havePrev = true;

            float gate = Mathf.Max(0.25f, ActiveGateSeconds);
            float elapsed = Time.time - _gateStart;
            if (elapsed >= gate)
            {
                _gateHz = _crossings / Mathf.Max(1e-4f, elapsed);
                _crossings = 0;
                _gateStart = Time.time;
            }

            if (_pulseFlash > 0f)
                _pulseFlash = Mathf.Max(0f, _pulseFlash - Time.deltaTime * 4f);
        }

        void ResetGate()
        {
            _crossings = 0;
            _gateStart = Time.time;
            _gateHz = 0f;
            _periodHz = 0f;
            _lastCrossTime = 0f;
            _havePrev = false;
            _prevSample = 0f;
            _pulseFlash = 0f;
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
            ResetGate();
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
                Color baseC = new Color(0.18f, 0.28f, 0.55f, 1f);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", baseC);
                _mat.color = baseC;
                if (_mat.HasProperty("_Metallic"))
                    _mat.SetFloat("_Metallic", 0.3f);
                if (_mat.HasProperty("_Smoothness"))
                    _mat.SetFloat("_Smoothness", 0.5f);
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
                Color faceC = new Color(0.05f, 0.08f, 0.12f, 1f);
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
            Color pulse = new Color(0.25f, 0.55f, 1f, 1f) * (0.1f + 1.4f * _pulseFlash);
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
            _readout.color = new Color(0.55f, 0.85f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 20f);
            _readout.text = "FREQ\nseeking circuit...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            string ch = ActiveChannel == Channel.Current ? "I" : "EMF";
            string gate = ActiveGateSeconds.ToString("0.#") + "s";

            if (_circuit == null)
            {
                _readout.text = "FREQ " + label + "\n" + ch + " gate " + gate + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON circuit" : (dist > connectWithinMeters ? "far" : "near");
            string hzGate = _gateHz.ToString("0.00") + "Hz";
            string hzPer = _periodHz > 0.01f ? _periodHz.ToString("0.00") + "Hz" : "-";
            string live;
            if (ActiveChannel == Channel.Current)
            {
                if (Mathf.Abs(_liveSample) < 0.001f)
                    live = (_liveSample * 1000f).ToString("+0.00;-0.00;0.00") + "mA";
                else
                    live = _liveSample.ToString("+0.000;-0.000;0.000") + "A";
            }
            else
            {
                live = _liveSample.ToString("+0.00;-0.00;0.00") + "V";
            }

            float elapsed = Time.time - _gateStart;
            string fill = Mathf.Clamp01(elapsed / Mathf.Max(0.25f, ActiveGateSeconds)).ToString("0%");

            _readout.text =
                "FREQ " + label + "\n"
                + "gate " + hzGate + "  per " + hzPer + "\n"
                + ch + " " + live + "  fill " + fill + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger\n"
                + "[ideal ZC gate]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.55f, 1f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}