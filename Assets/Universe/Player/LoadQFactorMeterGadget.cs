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
    /// Grabbable MEASURE Q-factor meter: while near an InductionCircuit, shows classical
    /// series RLC Q0=(1/R)sqrt(L/C), bandwidth BW=f0/Q0, damping ZETA=1/(2Q0), or ENERGY 0.5LI^2+0.5CV^2
    /// (with peak Q0 hold).
    /// Honesty: ideal series RLC quality factor from classical InductionCircuit R / Series L / Series C —
    /// NOT real Q-meter, not network analyzer, not 3dB bandwidth sweep, not ring-down, not FFT.
    /// CLR zeros peak Q0, resets energy storage (Vc + I_L), snaps to Q.
    /// XR activate / N cycles preset; P steps backward. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadQFactorMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_QFactorMeter";
        public const string Honesty =
            "Ideal series RLC Q from classical InductionCircuit R / Series L / Series C (Q0=(1/R)sqrt(L/C), BW=f0/Q0, zeta=1/(2Q0)). NOT real Q-meter, not network analyzer, not 3dB bandwidth sweep, not ring-down, not FFT.";

        enum Mode { Q, Bw, Zeta, Energy, Clr }

        static readonly string[] PresetLabels = { "Q", "BW", "ZETA", "ENERGY", "CLR" };
        static readonly Mode[] PresetModes = { Mode.Q, Mode.Bw, Mode.Zeta, Mode.Energy, Mode.Clr };

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
        int _presetIndex; // default Q
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        float _liveR;
        float _liveL;
        float _liveC;
        float _liveF0;
        float _liveOmega0;
        float _liveQ0;
        float _liveBw;
        float _liveZeta;
        float _liveEnergy;
        float _liveIl;
        float _liveVc;
        bool _qValid;
        float _peakQ0;
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

            SampleQFactor();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        void SampleQFactor()
        {
            _linked = false;
            _liveR = 0f;
            _liveL = 0f;
            _liveC = 0f;
            _liveF0 = 0f;
            _liveOmega0 = 0f;
            _liveQ0 = 0f;
            _liveBw = 0f;
            _liveZeta = 0f;
            _liveEnergy = 0f;
            _liveIl = 0f;
            _liveVc = 0f;
            _qValid = false;

            if (_circuit == null)
                return;

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            if (dist > connectWithinMeters)
                return;

            _linked = true;
            _liveR = _circuit.TotalResistanceOhms;
            var coil = _circuit.Coil;
            _liveL = coil != null ? coil.SeriesInductance : 0f;
            _liveC = coil != null ? coil.SeriesCapacitance : 0f;
            _liveIl = _circuit.InductorCurrentAmperes;
            _liveVc = _circuit.CapacitorVolts;
            _liveEnergy = 0.5f * _liveL * _liveIl * _liveIl + 0.5f * _liveC * _liveVc * _liveVc;

            if (_liveL > 0f && _liveC > 0f)
            {
                _liveOmega0 = 1f / Mathf.Sqrt(_liveL * _liveC);
                _liveF0 = _liveOmega0 / (2f * Mathf.PI);
            }

            if (_liveR > 1e-9f && _liveL > 0f && _liveC > 0f)
            {
                _liveQ0 = (1f / _liveR) * Mathf.Sqrt(_liveL / _liveC);
                _qValid = _liveQ0 > 0f && !float.IsNaN(_liveQ0) && !float.IsInfinity(_liveQ0);
                if (_qValid)
                {
                    _liveBw = _liveF0 / _liveQ0;
                    _liveZeta = 1f / (2f * _liveQ0);
                    if (_liveQ0 > _peakQ0)
                        _peakQ0 = _liveQ0;
                }
            }

            _glow = Mathf.Clamp01(_qValid ? Mathf.Min(1f, _liveQ0 / 50f) + _liveEnergy * 2f : 0f);
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
                _peakQ0 = 0f;
                if (_circuit != null)
                    _circuit.ResetEnergyStorage();
                for (int i = 0; i < PresetModes.Length; i++)
                {
                    if (PresetModes[i] == Mode.Q)
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
                Color baseC = new Color(0.42f, 0.22f, 0.55f, 1f);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", baseC);
                _mat.color = baseC;
                if (_mat.HasProperty("_Metallic"))
                    _mat.SetFloat("_Metallic", 0.45f);
                if (_mat.HasProperty("_Smoothness"))
                    _mat.SetFloat("_Smoothness", 0.4f);
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
                Color faceC = new Color(0.12f, 0.06f, 0.16f, 1f);
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
            Color pulse = new Color(0.75f, 0.35f, 0.95f, 1f) * (0.08f + 1.15f * _glow);
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
            _readout.color = new Color(0.85f, 0.55f, 0.98f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "Q\nseeking circuit...";
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

        static string FormatEnergy(float j)
        {
            float a = Mathf.Abs(j);
            if (a < 1e-9f)
                return "0.00nJ";
            if (a < 1e-6f)
                return (j * 1e9f).ToString("0.##") + "nJ";
            if (a < 1e-3f)
                return (j * 1e6f).ToString("0.###") + "uJ";
            if (a < 1f)
                return (j * 1000f).ToString("0.###") + "mJ";
            return j.ToString("0.###") + "J";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "Q " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
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
                case Mode.Bw:
                    line = _qValid ? ("BW " + FormatHz(_liveBw)) : "BW ---";
                    break;
                case Mode.Zeta:
                    line = _qValid ? ("zeta " + _liveZeta.ToString("0.####")) : "zeta ---";
                    break;
                case Mode.Energy:
                    line = "E " + FormatEnergy(_liveEnergy);
                    break;
                case Mode.Clr:
                case Mode.Q:
                default:
                    line = _qValid ? ("Q0 " + _liveQ0.ToString("0.###")) : "Q0 ---";
                    break;
            }

            string aux;
            if (ActiveMode == Mode.Bw)
                aux = "f0 " + FormatHz(_liveF0) + " Q0 " + (_qValid ? _liveQ0.ToString("0.##") : "---");
            else if (ActiveMode == Mode.Zeta)
                aux = "Q0 " + (_qValid ? _liveQ0.ToString("0.##") : "---") + " R " + FormatOhms(_liveR);
            else if (ActiveMode == Mode.Energy)
                aux = "IL " + _liveIl.ToString("0.###") + "A Vc " + _liveVc.ToString("0.###") + "V";
            else
                aux = "pkQ " + _peakQ0.ToString("0.##") + " R " + FormatOhms(_liveR);

            _readout.text =
                "Q " + label + "\n"
                + line + "\n"
                + aux + "\n"
                + "f0 " + FormatHz(_liveF0) + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger\n"
                + "[ideal series RLC Q]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.75f, 0.35f, 0.95f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
