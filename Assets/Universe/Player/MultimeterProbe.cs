using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Experiments;

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable MEASURE multimeter: live classical Faraday readout from InductionCircuit.
    /// Honesty: lumped dipole/coil model â€” not a real DMM or quantum state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MultimeterProbe : MonoBehaviour
    {
        public const string RootName = "Gadget_Multimeter";
        public const string Honesty = "Classical Faraday: Emf = -dPhi/dt, I = Emf / R. Lumped model.";

        TextMeshPro _readout;
        InductionCircuit _circuit;
        float _refreshAt;

        public void EnsureBuilt()
        {
            if (_readout == null)
                BuildReadout();
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
            if (_circuit == null)
                EnsureLabCircuit();
            RefreshText();
        }

        void EnsureLabCircuit()
        {
            _circuit = Object.FindFirstObjectByType<InductionCircuit>(FindObjectsInactive.Include);
            if (_circuit != null)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            _circuit = Object.FindFirstObjectByType<InductionCircuit>(FindObjectsInactive.Include);
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

            _readout.text =
                "EMF " + _circuit.EmfVolts.ToString("0.###") + " V\n"
                + "I " + _circuit.CurrentAmperes.ToString("0.####") + " A\n"
                + "Phi " + _circuit.FluxWebers.ToString("0.####") + " Wb\n"
                + "dPhi/dt " + _circuit.FluxRateWebersPerSecond.ToString("0.###") + "\n"
                + "[classical Faraday]";
        }
    }
}
