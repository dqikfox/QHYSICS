using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Experiments;

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable MEASURE multimeter: live classical Faraday readout from the nearest InductionCircuit.
    /// Honesty: lumped dipole/coil model — not a real DMM or quantum state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MultimeterProbe : MonoBehaviour
    {
        public const string RootName = "Gadget_Multimeter";
        public const string Honesty = "Classical Faraday: Emf = -dPhi/dt, I = Emf / R. Lumped model.";

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0f, 0.06f);

        [SerializeField, Tooltip("Max distance (m) to prefer a circuit; beyond this, still bind the closest if any exist.")]
        float preferWithinMeters = 1.25f;

        TextMeshPro _readout;
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        float _refreshAt;
        float _cacheAt;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public InductionCircuit BoundCircuit => _circuit;

        public void EnsureBuilt()
        {
            if (_readout == null)
                BuildReadout();
            EnsureCircuits(force: true);
            BindNearest();
            if (_circuit == null)
                EnsureLabCircuit();
        }

        void Awake()
        {
            EnsureBuilt();
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
            RefreshText();
        }

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
                _readout.text = "MULTIMETER\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            _readout.text =
                "EMF " + _circuit.EmfVolts.ToString("0.###") + " V\n"
                + "I " + _circuit.CurrentAmperes.ToString("0.####") + " A\n"
                + "Phi " + _circuit.FluxWebers.ToString("0.####") + " Wb\n"
                + "dPhi/dt " + _circuit.FluxRateWebersPerSecond.ToString("0.###") + "\n"
                + target + " " + dist.ToString("0.00") + "m" + (dist > preferWithinMeters ? " far" : "") + "\n"
                + "[classical Faraday]";
        }
    }
}
