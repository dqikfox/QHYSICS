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
    /// Grabbable MEASURE inductance meter: while near an InductionCircuit, shows classical
    /// series L (H), inductor current I_L, or energy 0.5*L*I^2.
    /// Honesty: ideal lumped SeriesInductance / InductorCurrentAmperes from InductionCircuit —
    /// NOT real LCR meter, not Q-meter, not impedance analyzer, not mutual inductance meter.
    /// Does not modify L (BUILD Inductor owns L). CLR resets I_L only. Pair with Inductor / Function Generator / Magnet.
    /// XR activate / N cycles preset; P steps backward. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadInductanceMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_InductanceMeter";
        public const string Honesty =
            "Ideal inductance meter from classical InductionCoil.SeriesInductance / InductionCircuit.InductorCurrentAmperes / 0.5LI^2. NOT real LCR meter, not Q-meter, not impedance analyzer, not mutual inductance meter.";

        enum Mode { L, Il, Energy, Clr }

        static readonly string[] PresetLabels = { "L", "IL", "ENERGY", "CLR" };
        static readonly Mode[] PresetModes = { Mode.L, Mode.Il, Mode.Energy, Mode.Clr };

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
        int _presetIndex; // default L
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        float _liveL;
        float _liveIl;
        float _liveEnergy;
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

            SampleInductance();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        void SampleInductance()
        {
            _linked = false;
            _liveL = 0f;
            _liveIl = 0f;
            _liveEnergy = 0f;
            if (_circuit == null)
                return;

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            if (dist > connectWithinMeters)
                return;

            _linked = true;
            var coil = _circuit.Coil;
            _liveL = coil != null ? coil.SeriesInductance : 0f;
            _liveIl = _circuit.InductorCurrentAmperes;
            if (_liveL > 0f)
                _liveEnergy = 0.5f * _liveL * _liveIl * _liveIl;

            float glowSrc = Mathf.Abs(_liveIl) > 1e-4f ? Mathf.Abs(_liveIl) : (_liveL * 1000f);
            _glow = Mathf.Clamp01(glowSrc / 12f);
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
                    _circuit.ResetInductorCurrent();
                for (int i = 0; i < PresetModes.Length; i++)
                {
                    if (PresetModes[i] == Mode.L)
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
                Color baseC = new Color(0.45f, 0.28f, 0.72f, 1f);
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
                Color faceC = new Color(0.08f, 0.04f, 0.14f, 1f);
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
            Color pulse = new Color(0.7f, 0.45f, 1f, 1f) * (0.08f + 1.2f * _glow);
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
            _readout.color = new Color(0.85f, 0.7f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "IND\nseeking circuit...";
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

        static string FormatAmps(float amps)
        {
            float a = Mathf.Abs(amps);
            if (a < 1e-6f)
                return "0.00uA";
            if (a < 0.001f)
                return (amps * 1e6f).ToString("+0.0;-0.0;0.0") + "uA";
            if (a < 1f)
                return (amps * 1000f).ToString("+0.00;-0.00;0.00") + "mA";
            if (a < 100f)
                return amps.ToString("+0.000;-0.000;0.000") + "A";
            return amps.ToString("+0.00;-0.00;0.00") + "A";
        }

        static string FormatJoules(float j)
        {
            float a = Mathf.Abs(j);
            if (a < 1e-6f)
                return "0 J";
            if (a < 1e-3f)
                return (j * 1e6f).ToString("0.###") + "uJ";
            if (a < 1f)
                return (j * 1e3f).ToString("0.###") + "mJ";
            return j.ToString("0.###") + "J";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "IND " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
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
                case Mode.Il:
                    line = "IL " + FormatAmps(_liveIl);
                    break;
                case Mode.Energy:
                    line = _liveL > 0f ? ("E " + FormatJoules(_liveEnergy)) : "E --- (L=0)";
                    break;
                case Mode.Clr:
                case Mode.L:
                default:
                    line = "L " + FormatHenries(_liveL);
                    break;
            }

            string aux;
            if (ActiveMode == Mode.Il)
                aux = "L " + FormatHenries(_liveL);
            else if (ActiveMode == Mode.Energy)
                aux = "IL " + FormatAmps(_liveIl);
            else
                aux = "IL " + FormatAmps(_liveIl);

            _readout.text =
                "IND " + label + "\n"
                + line + "\n"
                + aux + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger\n"
                + "[ideal L / IL / 0.5LI^2]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.45f, 1f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}