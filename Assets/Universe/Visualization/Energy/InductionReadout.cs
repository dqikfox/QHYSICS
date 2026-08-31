using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// World-space TMP panel of Φ, dΦ/dt, EMF, I plus the classical-model disclaimer and Field Lens layer.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(40)]
    public sealed class InductionReadout : MonoBehaviour
    {
        public const string Disclaimer =
            "Classical model: two-pole magnet, lumped Faraday law. Visualization of B is a sampled field, not a photograph of reality.";

        [SerializeField]
        [Tooltip("Circuit whose readings are displayed. Cached; not looked up per frame.")]
        InductionCircuit circuit;

        [SerializeField]
        [Tooltip("TMP text. Created at runtime if left empty.")]
        TextMeshPro text;

        [SerializeField]
        [Tooltip("How often to rebuild the string. Readings still come from the latest sim step.")]
        float refreshSeconds = 0.05f;

        [SerializeField]
        [Tooltip("If true, the panel yaws to face the cached camera (no continuous forced locomotion).")]
        bool billboardYaw = true;

        InductionCircuit _circuit;
        TextMeshPro _text;
        Camera _camera;
        FieldLens _lens;
        float _nextRefresh;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(768);

        public void SetCircuit(InductionCircuit value)
        {
            circuit = value;
            _circuit = value;
        }

        public void Bind(InductionCircuit value, TextMeshPro tmp)
        {
            SetCircuit(value);
            text = tmp;
            _text = tmp;
        }

        public void SetFieldLens(FieldLens lens)
        {
            _lens = lens;
        }

        public TextMeshPro Text => _text != null ? _text : text;

        void Awake()
        {
            _circuit = circuit;
            _text = text != null ? text : GetComponent<TextMeshPro>();
            if (_text == null)
                _text = GetComponentInChildren<TextMeshPro>();
            CacheCamera();
        }

        void CacheCamera()
        {
            _camera = Camera.main;
        }

        void LateUpdate()
        {
            if (_circuit == null)
                _circuit = circuit;
            if (_text == null)
                return;

            if (billboardYaw)
            {
                if (_camera == null)
                    CacheCamera();
                if (_camera != null)
                {
                    Vector3 toCam = _text.transform.position - _camera.transform.position;
                    toCam.y = 0f;
                    if (toCam.sqrMagnitude > 1e-6f)
                        _text.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                }
            }

            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + Mathf.Max(0.016f, refreshSeconds);
            RebuildText();
        }

        void RebuildText()
        {
            _sb.Length = 0;
            _sb.Append("INDUCTION LAB  v0.4\n");
            _sb.Append("Faraday law  EMF = -N dΦ/dt\n");
            if (_lens != null)
            {
                _sb.Append("Field Lens  ").Append(_lens.CurrentLayerName);
                _sb.Append("  [").Append(_lens.CurrentHonestyTag).Append("]\n");
                if (_lens.Focused != null)
                    _sb.Append("Aim  ").Append(_lens.Focused.KindName).Append('\n');
            }
            _sb.Append("timeScale ").Append(Time.timeScale.ToString("0.###"));
            if (Time.timeScale <= 0f)
                _sb.Append("  PAUSED");
            _sb.Append('\n');

            if (_circuit == null)
            {
                _sb.Append("no circuit\n");
            }
            else
            {
                AppendSci("Φ    ", _circuit.FluxWebers, " Wb");
                AppendSci("dΦ/dt", _circuit.FluxRateWebersPerSecond, " Wb/s");
                AppendSci("EMF  ", _circuit.EmfVolts, " V");
                AppendSci("I    ", _circuit.CurrentAmperes, " A");
                AppendSci("Pload", _circuit.LoadPowerWatts, " W");
                _sb.Append("Rtot ").Append(_circuit.TotalResistanceOhms.ToString("0.###")).Append(" Ω\n");
            }

            _sb.Append('\n');
            _sb.Append(Disclaimer);
            if (_lens != null)
            {
                _sb.Append("\nField Lens: ").Append(_lens.CurrentLayerName);
                _sb.Append(" — ").Append(_lens.CurrentHonestyTag);
                _sb.Append(". Never a literal quantum state.");
                if (_lens.CurrentLayerEnum == FieldLensLayer.EnergyFlow)
                    _sb.Append(" Energy flow is an educational approximation, not a full EM energy-flow solver.");
            }
            _text.text = _sb.ToString();
        }

        void AppendSci(string label, float value, string unit)
        {
            _sb.Append(label).Append(' ');
            float abs = Mathf.Abs(value);
            if (abs > 0f && (abs < 1e-3f || abs >= 1e3f))
                _sb.Append(value.ToString("0.000e+0"));
            else
                _sb.Append(value.ToString("0.0000"));
            _sb.Append(unit).Append('\n');
        }
    }
}