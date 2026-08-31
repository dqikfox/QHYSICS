using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Azimuthal induced E from lumped Faraday: ∮ E·dl = EMF/N, |E| ≈ |EMF| / (N 2π r).
    /// Sampled live from InductionCoil/InductionCircuit. Classical model / numerical sample.
    /// Not a Maxwell solver.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class ElectricFieldViz : MonoBehaviour
    {
        InductionCircuit _circuit;
        MagneticDipole _dipole;
        FieldLensTargetKind _kind;
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
            var go = new GameObject("EArrows");
            go.transform.SetParent(transform, false);
            _arrows = go.AddComponent<FieldArrowOverlay>();
            Color col = new Color(0.95f, 0.85f, 0.2f, 0.95f);
            _arrows.EnsureBuilt(_kind == FieldLensTargetKind.Load ? 8 : 16, col, 0.0032f);
            _caption = MakeCaption();
            ApplyVisibility();
        }

        void LateUpdate()
        {
            if (!_visible || !_built || _arrows == null)
                return;

            if (_kind == FieldLensTargetKind.Load)
                DrawLoadE();
            else if (_kind == FieldLensTargetKind.Coil)
                DrawCoilE();
            else
                DrawMagnetHint();
        }

        void DrawCoilE()
        {
            InductionCoil coil = _circuit != null ? _circuit.Coil : null;
            if (coil == null)
            {
                _arrows.HideAll();
                SetCaption("Electric field (classical lumped Faraday)\nNo coil.");
                return;
            }

            float emf = _circuit.EmfVolts;
            float nTurns = Mathf.Max(1f, coil.Turns);
            float absEmf = Mathf.Abs(emf);
            bool live = absEmf > 1e-6f;
            Vector3 origin = coil.transform.position;
            Vector3 normal = coil.AreaNormal;
            float R = coil.Radius;
            int count = _arrows.Count;
            float sign = emf >= 0f ? 1f : -1f;

            for (int k = 0; k < count; k++)
            {
                float a = (k / (float)count) * Mathf.PI * 2f;
                float r = R * (0.7f + 0.35f * ((k % 2) == 0 ? 1f : 0f));
                Vector3 radial = coil.transform.right * Mathf.Cos(a) + coil.transform.forward * Mathf.Sin(a);
                Vector3 p = origin + radial * r;
                Vector3 tangent = Vector3.Cross(normal, radial);
                if (tangent.sqrMagnitude < 1e-10f)
                {
                    _arrows.Hide(k);
                    continue;
                }

                tangent.Normalize();
                // ∮ E·dl = EMF/N  =>  |E| ≈ |EMF| / (N 2π r)   (classical lumped)
                float eMag = absEmf / (nTurns * 2f * Mathf.PI * Mathf.Max(r, 0.01f));
                Vector3 e = tangent * (sign * eMag);
                float vis = live ? Mathf.Lerp(0.012f, 0.045f, Mathf.Clamp01(absEmf / 0.05f)) : 0f;
                if (live)
                    _arrows.SetArrow(k, p, e, vis);
                else
                    _arrows.Hide(k);
            }

            if (live)
                SetCaption("E (azimuthal) from lumped Faraday\n∮ E·dl = EMF/N   Classical model / Numerical sample\nNot a Maxwell solver.");
            else
                SetCaption("E around coil — EMF ~ 0 (move the magnet)\nClassical model / Numerical sample");
        }

        void DrawLoadE()
        {
            if (_circuit == null)
            {
                _arrows.HideAll();
                return;
            }

            float i = _circuit.CurrentAmperes;
            float rLoad = _circuit.Coil != null ? _circuit.Coil.LoadResistance : 8f;
            float v = i * rLoad;
            float absV = Mathf.Abs(v);
            bool live = absV > 1e-6f;
            Transform t = transform;
            Vector3 axis = t.up * (v >= 0f ? 1f : -1f);
            int count = _arrows.Count;
            for (int k = 0; k < count; k++)
            {
                float u = (k / (float)Mathf.Max(1, count - 1) - 0.5f) * 0.06f;
                Vector3 p = t.position + t.up * u;
                if (live)
                    _arrows.SetArrow(k, p, axis, Mathf.Lerp(0.01f, 0.04f, Mathf.Clamp01(absV / 0.05f)));
                else
                    _arrows.Hide(k);
            }

            if (live)
                SetCaption("E along load from V = I R_load (lumped)\nClassical model / Numerical sample. Not a Maxwell solver.");
            else
                SetCaption("E along load — I ~ 0\nClassical model / Numerical sample");
        }

        void DrawMagnetHint()
        {
            _arrows.HideAll();
            SetCaption("Induced E is sampled at the coil (Faraday loop)\nMagnet is the B source. Classical model.");
        }

        void ApplyVisibility()
        {
            if (_arrows != null)
                _arrows.Visible = _visible;
            if (_caption != null)
                _caption.gameObject.SetActive(_visible);
        }

        TextMeshPro MakeCaption()
        {
            var go = new GameObject("Caption");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            go.transform.localScale = Vector3.one * 0.018f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.92f, 0.45f);
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