using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Core;
using RealityEngine.Visualization;

namespace RealityEngine.Physics.Thermo
{
    /// <summary>
    /// Reality Engine v1.0 shared conservation ledger. Coil + cell + heat on one board.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    public sealed class ConservationBoard : MonoBehaviour
    {
        public const string Honesty = ThermoEnergy.Honesty;

        [SerializeField]
        TextMeshPro text;

        [SerializeField]
        bool billboardYaw = true;

        InductionCircuit _circuit;
        HeatCoupler _heat;
        FieldLens _lens;
        ScaleEngine _scale;
        TextMeshPro _text;
        Camera _camera;
        float _nextRefresh;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(2800);

        public void Bind(InductionCircuit circuit, HeatCoupler heat, FieldLens lens, ScaleEngine scale, TextMeshPro tmp)
        {
            _circuit = circuit;
            _heat = heat;
            _lens = lens;
            _scale = scale;
            text = tmp;
            _text = tmp;
        }

        public TextMeshPro Text => _text != null ? _text : text;

        void Awake()
        {
            _text = text != null ? text : GetComponent<TextMeshPro>();
            if (_text == null)
                _text = GetComponentInChildren<TextMeshPro>();
            _camera = Camera.main;
        }

        void LateUpdate()
        {
            if (_text == null)
                return;

            if (billboardYaw)
            {
                if (_camera == null)
                    _camera = Camera.main;
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
            _nextRefresh = Time.unscaledTime + 0.10f;
            Rebuild();
        }

        void Rebuild()
        {
            if (_circuit == null)
                _circuit = FindFirstObjectByType<InductionCircuit>();
            if (_heat == null)
                _heat = FindFirstObjectByType<HeatCoupler>();

            float qIn = 0f, eta = 0f, captured = 0f, losses = 0f;
            bool closed = false;
            if (_heat != null)
            {
                qIn = _heat.QinPerSecond;
                eta = _heat.Eta;
                captured = _heat.CapturedPerSecond;
                losses = _heat.LossesPerSecond;
                closed = _heat.PathClosed;
            }

            _sb.Length = 0;
            _sb.Append("CONSERVATION LEDGER  v1.0  ").Append(ThermoEnergy.LabName).Append('\n');
            _sb.Append(Honesty).Append('\n');
            _sb.Append("North star: energy is a gradient, not a created substance.\n");
            _sb.Append('\n');
            _sb.Append(ThermoEnergy.CoilInstantPowerLines(_circuit)).Append('\n');
            _sb.Append('\n');
            _sb.Append(ThermoEnergy.CellAccountLines()).Append('\n');
            _sb.Append('\n');
            _sb.Append(ThermoEnergy.AccountLines(qIn, eta, captured, losses, closed)).Append('\n');
            _sb.Append('\n');
            _sb.Append(ThermoEnergy.Footer()).Append('\n');
            _sb.Append('\n');
            _sb.Append(ThermoEnergy.IntegrityLines()).Append('\n');
            if (_lens != null)
                _sb.Append("Field Lens  ").Append(_lens.CurrentLayerName).Append("  ").Append(_lens.CurrentHonestyTag).Append('\n');
            if (_scale != null)
                _sb.Append("Scale Engine  ").Append(_scale.CurrentScaleName).Append('\n');
            _sb.Append("Grab the heat coupler. Close a hot to cold path. Key 9 = Q6.\n");
            _sb.Append(Honesty);
            _text.text = _sb.ToString();
        }
    }
}
