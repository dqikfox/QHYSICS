using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Educational Poynting overlay S = E × H around the current-carrying loop.
    /// E is the lumped Faraday azimuthal field; H = B/μ0 from MagneticDipole.CalculateFieldAt
    /// (and a lumped I/(2πr) H around the load). Not a full EM energy-flow solver.
    /// Arrow length is scaled for VR, not |S| in W/m².
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class PoyntingViz : MonoBehaviour
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
            var go = new GameObject("SArrows");
            go.transform.SetParent(transform, false);
            _arrows = go.AddComponent<FieldArrowOverlay>();
            _arrows.EnsureBuilt(_kind == FieldLensTargetKind.Load ? 10 : 16, new Color(1f, 0.45f, 0.15f, 0.95f), 0.0032f);
            _caption = MakeCaption();
            ApplyVisibility();
        }

        void LateUpdate()
        {
            if (!_visible || !_built || _arrows == null)
                return;
            if (_kind == FieldLensTargetKind.Load)
                DrawLoadS();
            else if (_kind == FieldLensTargetKind.Coil)
                DrawLoopS();
            else
            {
                _arrows.HideAll();
                SetCaption("Energy flow is drawn around the current loop\nEducational approximation. Classical model.");
            }
        }

        void DrawLoopS()
        {
            InductionCoil coil = _circuit != null ? _circuit.Coil : null;
            if (coil == null || _dipole == null)
            {
                _arrows.HideAll();
                SetCaption("Poynting S = E × H needs coil + magnet.");
                return;
            }

            float emf = _circuit.EmfVolts;
            float absEmf = Mathf.Abs(emf);
            bool live = absEmf > 1e-6f || Mathf.Abs(_circuit.CurrentAmperes) > 1e-6f;
            float nTurns = Mathf.Max(1f, coil.Turns);
            float mu0 = 4f * Mathf.PI * MagneticDipole.Mu0Over4Pi;
            Vector3 origin = coil.transform.position;
            Vector3 normal = coil.AreaNormal;
            float R = coil.Radius;
            int count = _arrows.Count;
            float sign = emf >= 0f ? 1f : -1f;

            for (int k = 0; k < count; k++)
            {
                float a = (k / (float)count) * Mathf.PI * 2f;
                Vector3 radial = coil.transform.right * Mathf.Cos(a) + coil.transform.forward * Mathf.Sin(a);
                Vector3 p = origin + radial * R;
                Vector3 tangent = Vector3.Cross(normal, radial);
                if (tangent.sqrMagnitude < 1e-10f)
                {
                    _arrows.Hide(k);
                    continue;
                }

                tangent.Normalize();
                float eMag = absEmf / (nTurns * 2f * Mathf.PI * Mathf.Max(R, 0.01f));
                Vector3 e = tangent * (sign * eMag);
                Vector3 b = _dipole.CalculateFieldAt(p);
                Vector3 h = b / Mathf.Max(mu0, 1e-12f);
                Vector3 s = Vector3.Cross(e, h);
                if (!live || s.sqrMagnitude < 1e-24f)
                {
                    _arrows.Hide(k);
                    continue;
                }

                float vis = Mathf.Lerp(0.015f, 0.05f, Mathf.Clamp01(_circuit.NormalizedLoadCurrent));
                _arrows.SetArrow(k, p, s, vis);
            }

            if (live)
                SetCaption("S = E × H  (lumped E, sampled H = B/μ0)\nEducational approximation — not a full EM energy-flow solver\nArrow length scaled for VR. Classical model.");
            else
                SetCaption("S = E × H — EMF/I ~ 0 (move the magnet)\nEducational approximation. Classical model.");
        }

        void DrawLoadS()
        {
            if (_circuit == null)
            {
                _arrows.HideAll();
                return;
            }

            float i = _circuit.CurrentAmperes;
            bool live = Mathf.Abs(i) > 1e-6f;
            Transform t = transform;
            Vector3 axis = t.up;
            // Lumped resistor: E along axis, H = I φ-hat / (2π r), S inward.
            Vector3 eDir = axis * (i >= 0f ? 1f : -1f);
            int count = _arrows.Count;
            for (int k = 0; k < count; k++)
            {
                float a = (k / (float)count) * Mathf.PI * 2f;
                Vector3 radial = t.right * Mathf.Cos(a) + t.forward * Mathf.Sin(a);
                Vector3 p = t.position + radial * 0.04f;
                Vector3 hDir = Vector3.Cross(axis, radial);
                if (hDir.sqrMagnitude < 1e-10f)
                {
                    _arrows.Hide(k);
                    continue;
                }

                hDir.Normalize();
                if (i < 0f)
                    hDir = -hDir;
                Vector3 s = Vector3.Cross(eDir, hDir); // inward toward the load
                if (live)
                    _arrows.SetArrow(k, p, s, Mathf.Lerp(0.012f, 0.035f, _circuit.NormalizedLoadCurrent));
                else
                    _arrows.Hide(k);
            }

            if (live)
                SetCaption("Load: lumped S into resistor (E along V, H from I)\nEducational approximation — not a full EM energy-flow solver.");
            else
                SetCaption("Load Poynting — I ~ 0\nEducational approximation. Classical model.");
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
            go.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            go.transform.localScale = Vector3.one * 0.018f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.7f, 0.4f);
            tmp.rectTransform.sizeDelta = new Vector2(22f, 6f);
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