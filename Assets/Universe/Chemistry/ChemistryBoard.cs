using UnityEngine;
using TMPro;
using RealityEngine.Core;
using RealityEngine.Visualization;

namespace RealityEngine.Chemistry
{
    /// <summary>
    /// World-space Cu periodic snippet + coil conductivity one-liner.
    /// Honesty: conceptual chemistry, not QM.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(57)]
    public sealed class ChemistryBoard : MonoBehaviour
    {
        public const string Honesty = Element.ConceptualHonesty;

        [SerializeField]
        TextMeshPro text;

        [SerializeField]
        bool billboardYaw = true;

        FieldLens _lens;
        ScaleEngine _scale;
        TextMeshPro _text;
        Camera _camera;
        float _nextRefresh;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(1600);

        public void Bind(FieldLens lens, ScaleEngine scale, TextMeshPro tmp)
        {
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
            _nextRefresh = Time.unscaledTime + 0.12f;
            Rebuild();
        }

        void Rebuild()
        {
            _sb.Length = 0;
            _sb.Append("CHEMISTRY  v0.8  Cu slice\n");
            _sb.Append(Honesty).Append('\n');
            _sb.Append(Element.Cu.PeriodicCard()).Append('\n');
            _sb.Append("Magnet metals (optional): ").Append(Element.Nd.Symbol);
            _sb.Append(" Z=").Append(Element.Nd.Z);
            _sb.Append("  ").Append(Element.Fe.Symbol);
            _sb.Append(" Z=").Append(Element.Fe.Z).Append('\n');
            _sb.Append(Element.Nd.BondingOneLiner).Append('\n');
            _sb.Append(Element.ClassicalCoilHonesty).Append('\n');
            if (_lens != null)
            {
                _sb.Append("Field Lens  ").Append(_lens.CurrentLayerName);
                _sb.Append("  ").Append(_lens.CurrentHonestyTag).Append('\n');
            }
            if (_scale != null)
            {
                _sb.Append("Scale  ").Append(_scale.CurrentScaleName);
                _sb.Append("  ").Append(_scale.CurrentHonestyTag).Append('\n');
                if (_scale.CurrentScaleEnum == ScaleLevel.Atomic || _scale.CurrentScaleEnum == ScaleLevel.Molecular)
                    _sb.Append("Cu card is the Scale Atomic / Molecular look-at. Lattice/charge viz is schematic.\n");
            }
            _sb.Append("Look at this card or aim Field Lens Atomic / Scale Atomic at the copper coil.\n");
            _sb.Append("Q4 on the scientist: Why is copper a conductor?\n");
            _sb.Append(Honesty);
            _text.text = _sb.ToString();
        }
    }
}