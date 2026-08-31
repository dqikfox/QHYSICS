using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Plus/minus glyphs from the classical two-pole model, or conventional-current arrows from live I.
    /// Conceptual visualization — not a measured charge density, not a quantum state.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class ChargeDistributionViz : MonoBehaviour
    {
        MagneticDipole _dipole;
        InductionCircuit _circuit;
        FieldLensTargetKind _kind;
        TextMeshPro _plus;
        TextMeshPro _minus;
        FieldArrowOverlay _arrows;
        TextMeshPro _caption;
        bool _visible;
        bool _built;

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                ApplyVisibility();
            }
        }

        public void Configure(FieldLensTargetKind kind, MagneticDipole dipole, InductionCircuit circuit)
        {
            _kind = kind;
            _dipole = dipole;
            _circuit = circuit;
            EnsureBuilt();
        }

        void EnsureBuilt()
        {
            if (_built)
                return;
            _built = true;

            if (_kind == FieldLensTargetKind.Magnet)
            {
                _plus = MakeGlyph("+", new Color(1f, 0.35f, 0.3f));
                _minus = MakeGlyph("-", new Color(0.35f, 0.55f, 1f));
            }
            else if (_kind == FieldLensTargetKind.Load)
            {
                _plus = MakeGlyph("+", new Color(1f, 0.75f, 0.2f));
                _minus = MakeGlyph("-", new Color(0.4f, 0.8f, 1f));
            }
            else
            {
                var go = new GameObject("CurrentArrows");
                go.transform.SetParent(transform, false);
                _arrows = go.AddComponent<FieldArrowOverlay>();
                _arrows.EnsureBuilt(12, new Color(1f, 0.82f, 0.2f, 0.95f), 0.003f);
            }

            _caption = MakeCaption();
            ApplyVisibility();
        }

        void LateUpdate()
        {
            if (!_visible || !_built)
                return;

            if (_kind == FieldLensTargetKind.Magnet)
                PlaceMagnetPoles();
            else if (_kind == FieldLensTargetKind.Load)
                PlaceLoadPolarity();
            else
                PlaceCoilCurrent();
        }

        void PlaceMagnetPoles()
        {
            if (_dipole == null)
                return;
            if (_plus != null)
            {
                _plus.transform.position = _dipole.NorthPoleWorldPosition + Vector3.up * 0.02f;
                _plus.text = "+";
            }
            if (_minus != null)
            {
                _minus.transform.position = _dipole.SouthPoleWorldPosition - Vector3.up * 0.02f;
                _minus.text = "-";
            }

            SetCaption("Fictitious magnetic charge ±q_m = ±m/L\nClassical two-pole model. Conceptual visualization.\nNot a quantum state.");
        }

        void PlaceLoadPolarity()
        {
            float i = _circuit != null ? _circuit.CurrentAmperes : 0f;
            Transform t = transform;
            Vector3 axis = t.up;
            float h = 0.04f;
            bool pos = i >= 0f;
            if (_plus != null)
                _plus.transform.position = t.position + axis * (pos ? h : -h);
            if (_minus != null)
                _minus.transform.position = t.position + axis * (pos ? -h : h);

            if (Mathf.Abs(i) < 1e-6f)
                SetCaption("Load polarity from I·R (lumped). |I| ~ 0\nConceptual visualization. Classical model.");
            else
                SetCaption("Load polarity from sign(I) (lumped circuit)\nConceptual visualization. Not a surface-charge solver.");
        }

        void PlaceCoilCurrent()
        {
            if (_arrows == null)
                return;
            InductionCoil coil = _circuit != null ? _circuit.Coil : GetComponentInParent<InductionCoil>();
            if (coil == null)
            {
                _arrows.HideAll();
                SetCaption("No coil current to display.");
                return;
            }

            float i = _circuit != null ? _circuit.CurrentAmperes : 0f;
            Vector3 n = coil.AreaNormal;
            Vector3 origin = coil.transform.position;
            float R = coil.Radius;
            int count = _arrows.Count;
            float sign = i >= 0f ? 1f : -1f;
            bool live = Mathf.Abs(i) > 1e-6f;

            for (int k = 0; k < count; k++)
            {
                float a = (k / (float)count) * Mathf.PI * 2f;
                Vector3 radial = coil.transform.right * Mathf.Cos(a) + coil.transform.forward * Mathf.Sin(a);
                Vector3 p = origin + radial * R;
                Vector3 tangent = Vector3.Cross(n, radial).normalized * sign;
                if (live)
                    _arrows.SetArrow(k, p, tangent, 0.03f);
                else
                    _arrows.Hide(k);
            }

            if (live)
                SetCaption("Conventional current arrows from live I\nConceptual visualization. Wire is electrically neutral overall.");
            else
                SetCaption("Conventional current arrows (I ~ 0)\nConceptual visualization. Not a quantum state.");
        }

        void ApplyVisibility()
        {
            if (_plus != null)
                _plus.gameObject.SetActive(_visible);
            if (_minus != null)
                _minus.gameObject.SetActive(_visible);
            if (_arrows != null)
                _arrows.Visible = _visible;
            if (_caption != null)
                _caption.gameObject.SetActive(_visible);
        }

        TextMeshPro MakeGlyph(string text, Color color)
        {
            var go = new GameObject("Glyph_" + text);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.05f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 10f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 4f);
            tmp.raycastTarget = false;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
            return tmp;
        }

        TextMeshPro MakeCaption()
        {
            var go = new GameObject("Caption");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            go.transform.localScale = Vector3.one * 0.018f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.95f, 0.9f, 0.75f);
            tmp.rectTransform.sizeDelta = new Vector2(22f, 5f);
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
            return tmp;
        }

        void SetCaption(string text)
        {
            if (_caption != null)
                _caption.text = text;
        }
    }
}