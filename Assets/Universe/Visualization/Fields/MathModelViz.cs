using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// World-space equations and assumptions for the v0.3/v0.4 lumped Faraday lab.
    /// Classical model. Not a Maxwell solver.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(55)]
    public sealed class MathModelViz : MonoBehaviour
    {
        InductionCircuit _circuit;
        MagneticDipole _dipole;
        TextMeshPro _text;
        bool _visible;
        bool _built;
        float _nextRefresh;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(1024);

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                if (_text != null)
                    _text.gameObject.SetActive(_visible);
            }
        }

        public void Bind(InductionCircuit circuit, MagneticDipole dipole)
        {
            _circuit = circuit;
            _dipole = dipole;
            EnsureBuilt();
        }

        void EnsureBuilt()
        {
            if (_built)
                return;
            _built = true;
            var go = new GameObject("MathText");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            _text = go.AddComponent<TextMeshPro>();
            _text.fontSize = 0.18f;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.color = new Color(0.85f, 0.95f, 1f);
            _text.rectTransform.sizeDelta = new Vector2(0.72f, 0.62f);
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.raycastTarget = false;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                _text.font = font;

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(transform, false);
            board.transform.localPosition = new Vector3(0.28f, -0.22f, 0.012f);
            board.transform.localScale = new Vector3(0.74f, 0.64f, 0.008f);
            var col = board.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                    Destroy(col);
                else
                    DestroyImmediate(col);
            }

            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null)
                s = Shader.Find("Sprites/Default");
            var mat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.04f, 0.06f, 0.1f));
            var r = board.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = mat;

            if (_text != null)
                _text.gameObject.SetActive(_visible);
        }

        void LateUpdate()
        {
            if (!_visible || _text == null)
                return;
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 0.08f;
            Rebuild();
        }

        void Rebuild()
        {
            _sb.Length = 0;
            _sb.Append("MATHEMATICAL MODEL\n");
            _sb.Append("Honesty: Classical model\n");
            _sb.Append("Not a Maxwell solver. Not a quantum state.\n\n");
            _sb.Append("Faraday (lumped N-turn coil)\n");
            _sb.Append("  EMF = -N dΦ/dt\n");
            _sb.Append("  Φ = ∫ B·dA   disk, normal = local +Y\n");
            if (_circuit != null && _circuit.Coil != null)
                _sb.Append("  N = ").Append(_circuit.Coil.Turns).Append('\n');
            _sb.Append('\n');
            _sb.Append("Two-pole B (fictitious ±q_m)\n");
            _sb.Append("  q_m = m / L\n");
            _sb.Append("  B(r) = (μ0/4π) q_m (r_N/|r_N|³ - r_S/|r_S|³)\n");
            if (_dipole != null)
            {
                _sb.Append("  m = ").Append(_dipole.magneticMoment.ToString("0.###")).Append(" A·m²\n");
                _sb.Append("  L = ").Append(_dipole.magnetLength.ToString("0.###")).Append(" m\n");
            }
            _sb.Append('\n');
            _sb.Append("Circuit (resistive, no L)\n");
            _sb.Append("  I = EMF / (R_w + R_L)\n");
            _sb.Append("  P = I² R_L\n");
            if (_circuit != null)
            {
                _sb.Append("  Φ = ").Append(Sci(_circuit.FluxWebers)).Append(" Wb\n");
                _sb.Append("  dΦ/dt = ").Append(Sci(_circuit.FluxRateWebersPerSecond)).Append(" Wb/s\n");
                _sb.Append("  EMF = ").Append(Sci(_circuit.EmfVolts)).Append(" V\n");
                _sb.Append("  I = ").Append(Sci(_circuit.CurrentAmperes)).Append(" A\n");
                _sb.Append("  Rtot = ").Append(_circuit.TotalResistanceOhms.ToString("0.###")).Append(" Ω\n");
            }
            _sb.Append('\n');
            _sb.Append("Assumptions: vacuum μ0, no coil self-field,\n");
            _sb.Append("no displacement current, lumped EMF.\n");
            _text.text = _sb.ToString();
        }

        static string Sci(float value)
        {
            float abs = Mathf.Abs(value);
            if (abs > 0f && (abs < 1e-3f || abs >= 1e3f))
                return value.ToString("0.000e+0");
            return value.ToString("0.0000");
        }
    }
}