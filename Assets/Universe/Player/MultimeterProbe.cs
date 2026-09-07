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
    /// Grabbable MEASURE multimeter: live classical Faraday readout from the nearest InductionCircuit.
    /// Honesty: lumped dipole/coil model - not a real DMM or quantum state.
    /// XR activate / N-P / desktop LMB-scroll cycles display pages (ALL / EMF / I / LOAD).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MultimeterProbe : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Multimeter";
        public const string Honesty = "Classical Faraday: Emf = -dPhi/dt, I = Emf / R. Lumped model.";

        static readonly string[] ModeNames = { "ALL", "EMF", "I", "LOAD" };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0f, 0.06f);

        [SerializeField, Tooltip("Max distance (m) to prefer a circuit; beyond this, still bind the closest if any exist.")]
        float preferWithinMeters = 1.25f;

        TextMeshPro _readout;
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;

        CircuitComponent _legacyComponent;

        void BindLegacyCircuitComponent()
        {
            _legacyComponent = null;
            var lab = Object.FindFirstObjectByType<CircuitLab>(FindObjectsInactive.Include);
            if (lab == null) return;

            // Find nearest placed CircuitComponent
            CircuitComponent best = null;
            float bestDist = 999f;
            foreach (var cc in Object.FindObjectsByType<CircuitComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cc == null || !cc.IsPlaced) continue;
                float d = (cc.transform.position - TipWorld).sqrMagnitude;
                if (d < bestDist && d < 1.8f)
                {
                    bestDist = d;
                    best = cc;
                }
            }
            _legacyComponent = best;
        }

        XRGrabInteractable _grab;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _modeIndex;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public InductionCircuit BoundCircuit => _circuit;
        public string ActiveModeName => ModeNames[Mathf.Clamp(_modeIndex, 0, ModeNames.Length - 1)];

        public void EnsureBuilt()
        {
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            EnsureCircuits(force: true);
            BindNearest();
            if (_circuit == null)
                EnsureLabCircuit();
            if (_circuit == null)
                BindLegacyCircuitComponent();
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
        }

        void Update()
        {
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.08f;
            if (Time.unscaledTime >= _cacheAt || _circuits == null)
                EnsureCircuits(force: false);
            BindNearest();
            if (_circuit == null)
                EnsureLabCircuit();
            if (_circuit == null)
                BindLegacyCircuitComponent();
            else
                _legacyComponent = null;
            PollDesktopCycle();
            RefreshText();
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
            Cycle(1);
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
                Cycle(1);
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
            _modeIndex = (_modeIndex + delta + ModeNames.Length) % ModeNames.Length;
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
                // Never bind to self if a circuit somehow lives on this multimeter.
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
            // preferWithinMeters is advisory for readout only; we still bind closest so lab works from afar.
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
                go.transform.localPosition = new Vector3(0f, 0.045f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 28f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.55f, 0.95f, 0.75f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(28f, 16f);
            _readout.text = "MULTIMETER\nseeking circuit...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;
            if (_circuit == null)
            {
                if (_legacyComponent != null)
                {
                    float distL = Vector3.Distance(TipWorld, _legacyComponent.transform.position);
                    string name = _legacyComponent.name;
                    if (name.Length > 18)
                        name = name.Substring(0, 16) + "..";
                    double v = _legacyComponent.GetVoltage();
                    double iAmp = _legacyComponent.GetCurrentValue();
                    _readout.text =
                        "MULTIMETER " + ActiveModeName + "\n"
                        + "V " + v.ToString("0.###") + " V\n"
                        + "I " + iAmp.ToString("0.####") + " A\n"
                        + name + " " + distL.ToString("0.00") + "m\n"
                        + "[" + ActiveModeName + "]\n"
                        + "[CircuitLab component]";
                    return;
                }
                _readout.text =
                    "MULTIMETER " + ActiveModeName + "\n"
                    + "no InductionCircuit\n"
                    + "Enter Sandbox / Induction\n"
                    + "N/P or VR trigger cycle";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";
            string far = dist > preferWithinMeters ? " far" : "";
            string footer = target + " " + dist.ToString("0.00") + "m" + far;
            string mode = ActiveModeName;

            InductionCoil coil = _circuit.Coil;
            float rWinding = coil != null ? coil.Resistance : 0f;
            float rLoad = coil != null ? coil.LoadResistance : 0f;
            float extEmf = coil != null ? coil.ExternalSeriesEmf : 0f;

            switch (_modeIndex)
            {
                case 1: // EMF
                    _readout.text =
                        "EMF " + _circuit.EmfVolts.ToString("0.###") + " V\n"
                        + "dPhi/dt " + _circuit.FluxRateWebersPerSecond.ToString("0.###") + "\n"
                        + "Phi " + _circuit.FluxWebers.ToString("0.####") + " Wb\n"
                        + "Ext " + extEmf.ToString("0.##") + " V\n"
                        + footer + "\n"
                        + "[" + mode + " classical]";
                    break;
                case 2: // I
                    _readout.text =
                        "I " + _circuit.CurrentAmperes.ToString("0.####") + " A\n"
                        + "P " + _circuit.LoadPowerWatts.ToString("0.####") + " W\n"
                        + "|I|norm " + _circuit.NormalizedLoadCurrent.ToString("0.##") + "\n"
                        + footer + "\n"
                        + "[" + mode + " classical]";
                    break;
                case 3: // LOAD
                    _readout.text =
                        "Rtot " + _circuit.TotalResistanceOhms.ToString("0.##") + " ohm\n"
                        + "Rw " + rWinding.ToString("0.##") + " Rl " + rLoad.ToString("0.##") + "\n"
                        + "ExtEmf " + extEmf.ToString("0.##") + " V\n"
                        + "P " + _circuit.LoadPowerWatts.ToString("0.####") + " W\n"
                        + footer + "\n"
                        + "[" + mode + " classical]";
                    break;
                default: // ALL
                    _readout.text =
                        "EMF " + _circuit.EmfVolts.ToString("0.###") + " V\n"
                        + "I " + _circuit.CurrentAmperes.ToString("0.####") + " A\n"
                        + "Phi " + _circuit.FluxWebers.ToString("0.####") + " Wb\n"
                        + "dPhi/dt " + _circuit.FluxRateWebersPerSecond.ToString("0.###") + "\n"
                        + footer + "\n"
                        + "[" + mode + " classical]";
                    break;
            }
        }
    }
}
