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
    /// Grabbable MEASURE oscilloscope: while near an InductionCircuit, draws a classical strip chart of Emf/I/Phi.
    /// Honesty: ideal rolling strip from classical InductionCircuit Emf/I/Phi samples.
    /// NOT real ADC / triggered scope / FFT / probe capacitance.
    /// Does not modify the circuit. XR activate / N cycles preset; P steps backward.
    /// Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadOscilloscopeGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Oscilloscope";
        public const string Honesty =
            "Ideal rolling strip from classical InductionCircuit Emf/I/Phi samples. NOT real ADC / triggered scope / FFT / probe capacitance.";

        enum Channel { Emf, Current, Flux }

        static readonly string[] PresetLabels =
        {
            "EMF0.5", "EMF1", "EMF2",
            "I0.5", "I1", "I2",
            "Phi1", "Phi2"
        };
        static readonly Channel[] PresetChannels =
        {
            Channel.Emf, Channel.Emf, Channel.Emf,
            Channel.Current, Channel.Current, Channel.Current,
            Channel.Flux, Channel.Flux
        };
        static readonly float[] PresetSeconds =
        {
            0.5f, 1f, 2f,
            0.5f, 1f, 2f,
            1f, 2f
        };

        const int BufferSize = 64;

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay linked and sample.")]
        float connectWithinMeters = 0.85f;

        TextMeshPro _readout;
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        LineRenderer _wave;
        Transform _face;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        float _sampleAt;
        int _presetIndex = 1; // default EMF1 (1s)
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;
        Material _waveMat;

        readonly float[] _buffer = new float[BufferSize];
        int _write;
        int _count;
        float _live;
        float _peakAbs;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];
        public float ActiveSeconds => PresetSeconds[Mathf.Clamp(_presetIndex, 0, PresetSeconds.Length - 1)];
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
            ClearBuffer();
            RefreshWaveform();
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
            DestroyMat(ref _waveMat);
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

            _linked = false;
            _live = 0f;
            if (_circuit != null)
            {
                float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
                if (dist <= connectWithinMeters)
                {
                    _linked = true;
                    _live = ReadChannel(_circuit, ActiveChannel);
                    if (Time.unscaledTime >= _sampleAt)
                    {
                        float dt = Mathf.Max(0.005f, ActiveSeconds / BufferSize);
                        _sampleAt = Time.unscaledTime + dt;
                        PushSample(_live);
                    }
                }
            }

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.04f;
                RefreshWaveform();
                PollDesktopCycle();
                RefreshText();
            }
        }

        static float ReadChannel(InductionCircuit c, Channel ch)
        {
            switch (ch)
            {
                case Channel.Current: return c.CurrentAmperes;
                case Channel.Flux: return c.FluxWebers;
                default: return c.EmfVolts;
            }
        }

        void PushSample(float v)
        {
            _buffer[_write] = v;
            _write = (_write + 1) % BufferSize;
            if (_count < BufferSize)
                _count++;
        }

        void ClearBuffer()
        {
            for (int i = 0; i < BufferSize; i++)
                _buffer[i] = 0f;
            _write = 0;
            _count = 0;
            _peakAbs = 0f;
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
            CyclePreset(+1);
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
                CyclePreset(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                CyclePreset(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CyclePreset(int delta)
        {
            if (PresetLabels.Length == 0)
                return;
            _presetIndex = (_presetIndex + delta) % PresetLabels.Length;
            if (_presetIndex < 0)
                _presetIndex += PresetLabels.Length;
            ClearBuffer();
            RefreshWaveform();
            RefreshText();
        }

        public void DesktopActivate(int delta) => CyclePreset(delta);

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

        float ChannelFloor()
        {
            switch (ActiveChannel)
            {
                case Channel.Current: return 0.001f;
                case Channel.Flux: return 1e-6f;
                default: return 0.01f;
            }
        }

        void RefreshWaveform()
        {
            if (_wave == null)
                return;

            int n = Mathf.Max(2, _count > 0 ? _count : 2);
            _wave.positionCount = n;

            float peak = ChannelFloor();
            for (int i = 0; i < _count; i++)
            {
                int idx = (_write - _count + i + BufferSize) % BufferSize;
                float a = Mathf.Abs(_buffer[idx]);
                if (a > peak)
                    peak = a;
            }
            _peakAbs = peak;

            // Face local: X across face, Y up on face (after face Euler 90)
            float halfW = 0.055f;
            float halfH = 0.028f;
            for (int i = 0; i < n; i++)
            {
                float t = n <= 1 ? 0f : (float)i / (n - 1);
                float x = Mathf.Lerp(-halfW, halfW, t);
                float y = 0f;
                if (_count > 0)
                {
                    int si = Mathf.Clamp(Mathf.RoundToInt(t * (_count - 1)), 0, _count - 1);
                    int idx = (_write - _count + si + BufferSize) % BufferSize;
                    float norm = Mathf.Clamp(_buffer[idx] / peak, -1f, 1f);
                    y = norm * halfH;
                }
                // Local to gadget: face sits at (0, 0.035, 0.02), plate in XZ after 90° X rot —
                // draw in gadget local ahead of face: X across, Y up a bit, Z slightly in front.
                _wave.SetPosition(i, new Vector3(x, 0.038f + y, 0.028f));
            }
        }

        void EnsureVisual()
        {
            if (_bodyRenderer == null)
                _bodyRenderer = GetComponent<Renderer>();

            if (_mat == null && _bodyRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(sh);
                Color baseC = new Color(0.1f, 0.35f, 0.55f, 1f);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", baseC);
                _mat.color = baseC;
                if (_mat.HasProperty("_Metallic"))
                    _mat.SetFloat("_Metallic", 0.3f);
                if (_mat.HasProperty("_Smoothness"))
                    _mat.SetFloat("_Smoothness", 0.4f);
                _bodyRenderer.sharedMaterial = _mat;
            }

            _face = transform.Find("Face");
            if (_face == null)
            {
                var faceGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                faceGo.name = "Face";
                faceGo.transform.SetParent(transform, false);
                faceGo.transform.localPosition = new Vector3(0f, 0.035f, 0.02f);
                faceGo.transform.localRotation = Quaternion.identity;
                faceGo.transform.localScale = new Vector3(0.12f, 0.07f, 0.006f);
                Object.Destroy(faceGo.GetComponent<Collider>());
                var fr = faceGo.GetComponent<Renderer>();
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _faceMat = new Material(sh);
                Color faceC = new Color(0.05f, 0.07f, 0.09f, 1f);
                if (_faceMat.HasProperty("_BaseColor"))
                    _faceMat.SetColor("_BaseColor", faceC);
                _faceMat.color = faceC;
                fr.sharedMaterial = _faceMat;
                _face = faceGo.transform;
            }

            Transform waveT = transform.Find("Wave");
            if (waveT == null)
            {
                var waveGo = new GameObject("Wave");
                waveGo.transform.SetParent(transform, false);
                waveGo.transform.localPosition = Vector3.zero;
                waveGo.transform.localRotation = Quaternion.identity;
                waveGo.transform.localScale = Vector3.one;
                _wave = waveGo.AddComponent<LineRenderer>();
            }
            else
            {
                _wave = waveT.GetComponent<LineRenderer>();
                if (_wave == null)
                    _wave = waveT.gameObject.AddComponent<LineRenderer>();
            }

            _wave.useWorldSpace = false;
            _wave.loop = false;
            _wave.widthMultiplier = 0.0035f;
            _wave.numCapVertices = 2;
            _wave.numCornerVertices = 2;
            _wave.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _wave.receiveShadows = false;
            if (_waveMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _waveMat = new Material(sh);
                Color wc = new Color(0.2f, 0.95f, 0.75f, 1f);
                if (_waveMat.HasProperty("_BaseColor"))
                    _waveMat.SetColor("_BaseColor", wc);
                _waveMat.color = wc;
                if (_waveMat.HasProperty("_EmissionColor"))
                {
                    _waveMat.EnableKeyword("_EMISSION");
                    _waveMat.SetColor("_EmissionColor", wc * 0.6f);
                }
            }
            _wave.sharedMaterial = _waveMat;
            _wave.startColor = new Color(0.2f, 0.95f, 0.75f, 1f);
            _wave.endColor = new Color(0.15f, 0.7f, 0.9f, 1f);
            if (_wave.positionCount < 2)
            {
                _wave.positionCount = 2;
                _wave.SetPosition(0, new Vector3(-0.055f, 0.038f, 0.028f));
                _wave.SetPosition(1, new Vector3(0.055f, 0.038f, 0.028f));
            }
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
            _readout.fontSize = 22f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.65f, 0.9f, 0.95f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(36f, 20f);
            _readout.text = "SCOPE\nseeking circuit...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            float tb = ActiveSeconds;
            if (_circuit == null)
            {
                _readout.text = "SCOPE " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON circuit" : (dist > connectWithinMeters ? "far" : "near");
            string liveStr = FormatValue(_live, ActiveChannel);
            string peakStr = FormatValue(_peakAbs, ActiveChannel);

            _readout.text =
                "SCOPE " + label + " " + tb.ToString("0.#") + "s\n"
                + "live " + liveStr + "\n"
                + "peak " + peakStr + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger\n"
                + "[ideal strip]";
        }

        static string FormatValue(float v, Channel ch)
        {
            switch (ch)
            {
                case Channel.Current:
                    if (Mathf.Abs(v) < 0.001f)
                        return (v * 1000f).ToString("+0.00;-0.00;0.00") + "mA";
                    return v.ToString("+0.000;-0.000;0.000") + "A";
                case Channel.Flux:
                    return v.ToString("+0.000e0;-0.000e0;0") + "Wb";
                default:
                    return v.ToString("+0.000;-0.000;0.000") + "V";
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.15f, 0.55f, 0.8f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
