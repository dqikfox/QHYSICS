using UnityEngine;
using TMPro;
using RealityEngine.Core;
using RealityEngine.Visualization;
using RealityEngine.Chemistry;

namespace RealityEngine.Biology
{
    /// <summary>
    /// World-space biology scale ladder + toy energy account.
    /// Honesty on every ladder line: Conceptual visualization / Classical energy accounting.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(58)]
    public sealed class BiologyBoard : MonoBehaviour
    {
        public const string Honesty = BioEnergy.Honesty;

        [SerializeField]
        TextMeshPro text;

        [SerializeField]
        bool billboardYaw = true;

        FieldLens _lens;
        ScaleEngine _scale;
        MuscleCell _cell;
        TextMeshPro _text;
        Camera _camera;
        float _nextRefresh;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(2200);

        public void Bind(FieldLens lens, ScaleEngine scale, MuscleCell cell, TextMeshPro tmp)
        {
            _lens = lens;
            _scale = scale;
            _cell = cell;
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
            _sb.Append("BIOLOGY  v0.9  muscle / mitochondrion slice\n");
            _sb.Append(Honesty).Append('\n');
            _sb.Append(BioEnergy.NotSimulation).Append('\n');
            _sb.Append("SCALE LADDER\n");
            _sb.Append(BioEnergy.ScaleLadder()).Append('\n');
            if (_scale != null)
            {
                ScaleLevel s = _scale.CurrentScaleEnum;
                _sb.Append("Scale Engine  ").Append(_scale.CurrentScaleName);
                _sb.Append("  ->  ").Append(BioScale.RepresentationOf(s)).Append('\n');
                _sb.Append(BioScale.InForceOf(s)).Append('\n');
                _sb.Append(BioScale.HonestyOf(s)).Append('\n');
            }
            if (_lens != null)
            {
                _sb.Append("Field Lens  ").Append(_lens.CurrentLayerName);
                _sb.Append("  ").Append(_lens.CurrentHonestyTag).Append('\n');
                _sb.Append("Cell peel: Normal=blob  Material=membrane  Atomic=molecular schematic  Charge/Energy=account  Math=ATP hydrolysis conceptual\n");
            }
            _sb.Append(BioEnergy.AccountLines()).Append('\n');
            _sb.Append(BioEnergy.GradientContrast()).Append('\n');
            _sb.Append("Atoms  ").Append(Element.AtpFormula).Append("  (schematic). Coil chemistry is Cu in the same lab.\n");
            _sb.Append("Grab the cell. Scale +/- and Field Lens peel the same object. Q5 on the scientist: Where does muscle energy come from?\n");
            if (_cell != null)
                _sb.Append("Cell object present (grabbable).\n");
            _sb.Append(Honesty);
            _text.text = _sb.ToString();
        }
    }
}
