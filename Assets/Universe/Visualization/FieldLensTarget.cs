using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Core;
using RealityEngine.Chemistry;
using RealityEngine.Biology;

namespace RealityEngine.Visualization
{
    public enum FieldLensTargetKind
    {
        Magnet,
        Coil,
        Load,
        Cell
    }

    /// <summary>
    /// Per-object Field Lens peel. Enables/disables child viz per layer.
    /// Samples live sim (MagneticDipole.CalculateFieldAt, coil flux/EMF/I) — no decorative noise.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FieldLensTarget : MonoBehaviour
    {
        FieldLens _lens;
        FieldLensTargetKind _kind;
        MagneticDipole _dipole;
        InductionCircuit _circuit;
        MagneticFieldViz _magneticViz;
        Renderer[] _renderers;
        Material[] _original;
        Material[] _look;
        LatticeViz _lattice;
        ChargeDistributionViz _charge;
        ElectricFieldViz _electric;
        FieldArrowOverlay _bSample;
        PoyntingViz _poynting;
        TextMeshPro _honesty;
        XRGrabInteractable _grab;
        XRSimpleInteractable _simple;
        bool _xriWired;
        bool _configured;
        int _layer;
        int _scale;

        public FieldLensTargetKind Kind => _kind;
        public string KindName => _kind.ToString();
        public int Layer => _layer;

        public bool IsXrHovered
        {
            get
            {
                if (_grab != null && _grab.isHovered)
                    return true;
                if (_simple != null && _simple.isHovered)
                    return true;
                return false;
            }
        }

        public void Configure(
            FieldLens lens,
            FieldLensTargetKind kind,
            MagneticDipole dipole,
            InductionCircuit circuit,
            MagneticFieldViz magneticViz)
        {
            _lens = lens;
            _kind = kind;
            _dipole = dipole;
            _circuit = circuit;
            _magneticViz = magneticViz;
            if (!_configured)
            {
                CacheOriginalRenderers();
                BuildChildren();
                WireXri();
                _configured = true;
            }

            if (_lens != null)
                _lens.Register(this);
            ApplyLayer(_lens != null ? _lens.CurrentLayer : 0);
        }

        public void ConfigureCell(FieldLens lens)
        {
            Configure(lens, FieldLensTargetKind.Cell, null, null, null);
        }

        void OnDisable()
        {
            if (_lens != null)
                _lens.Unregister(this);
        }

        void OnEnable()
        {
            if (_configured && _lens != null)
                _lens.Register(this);
        }

        void CacheOriginalRenderers()
        {
            var rs = GetComponentsInChildren<Renderer>(true);
            int n = 0;
            for (int i = 0; i < rs.Length; i++)
            {
                if (KeepRenderer(rs[i]))
                    n++;
            }

            _renderers = new Renderer[n];
            _original = new Material[n];
            int w = 0;
            for (int i = 0; i < rs.Length; i++)
            {
                if (!KeepRenderer(rs[i]))
                    continue;
                _renderers[w] = rs[i];
                _original[w] = rs[i].sharedMaterial;
                w++;
            }
        }

        static bool KeepRenderer(Renderer r)
        {
            if (r == null || r is LineRenderer)
                return false;
            if (r.GetComponent<TMP_Text>() != null)
                return false;
            Transform t = r.transform;
            while (t != null)
            {
                string n = t.name;
                if (!string.IsNullOrEmpty(n) && n.StartsWith("Bio", System.StringComparison.Ordinal))
                    return false;
                t = t.parent;
            }
            return true;
        }

        void BuildChildren()
        {
            if (_kind == FieldLensTargetKind.Cell)
            {
                BuildHonestyLabel();
                BuildLookMaterials();
                return;
            }

            _lattice = MakeChild<LatticeViz>("LensLattice");
            if (_kind == FieldLensTargetKind.Magnet)
                _lattice.BuildMagnet(
                    _dipole != null ? _dipole.magnetLength : 0.12f,
                    _dipole != null ? _dipole.magnetRadius : 0.012f,
                    new Color(0.75f, 0.78f, 0.82f));
            else if (_kind == FieldLensTargetKind.Coil)
                _lattice.BuildCoil(
                    _circuit != null && _circuit.Coil != null ? _circuit.Coil.Radius : 0.15f,
                    new Color(0.82f, 0.52f, 0.22f));
            else
                _lattice.BuildLoad(new Color(0.55f, 0.45f, 0.35f));
            _lattice.Visible = false;

            _charge = MakeChild<ChargeDistributionViz>("LensCharge");
            _charge.Configure(_kind, _dipole, _circuit);
            _charge.Visible = false;

            _electric = MakeChild<ElectricFieldViz>("LensE");
            _electric.Configure(_kind, _dipole, _circuit);
            _electric.Visible = false;

            if (_kind == FieldLensTargetKind.Coil)
            {
                _bSample = MakeChild<FieldArrowOverlay>("LensBSample");
                _bSample.EnsureBuilt(16, new Color(0.25f, 0.95f, 0.55f, 0.95f), 0.003f);
                _bSample.Visible = false;
            }

            _poynting = MakeChild<PoyntingViz>("LensS");
            _poynting.Configure(_kind, _dipole, _circuit);
            _poynting.Visible = false;

            BuildHonestyLabel();
            BuildLookMaterials();
        }

        void BuildHonestyLabel()
        {
            Transform existing = transform.Find("LensHonesty");
            GameObject labelGo;
            if (existing != null)
                labelGo = existing.gameObject;
            else
            {
                labelGo = new GameObject("LensHonesty");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.localPosition = HonestyOffset();
                labelGo.transform.localScale = Vector3.one * 0.02f;
            }
            _honesty = labelGo.GetComponent<TextMeshPro>();
            if (_honesty == null)
                _honesty = labelGo.AddComponent<TextMeshPro>();
            _honesty.fontSize = 5.5f;
            _honesty.alignment = TextAlignmentOptions.Center;
            _honesty.color = new Color(0.92f, 0.95f, 0.85f);
            _honesty.rectTransform.sizeDelta = new Vector2(16f, 4f);
            _honesty.raycastTarget = false;
            _honesty.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                _honesty.font = font;
        }

        Vector3 HonestyOffset()
        {
            if (_kind == FieldLensTargetKind.Magnet)
                return new Vector3(0f, 0.14f, 0f);
            if (_kind == FieldLensTargetKind.Coil)
                return new Vector3(0f, 0.08f, 0f);
            if (_kind == FieldLensTargetKind.Cell)
                return new Vector3(0f, 0.16f, 0f);
            return new Vector3(0f, 0.08f, 0f);
        }

        T MakeChild<T>(string name) where T : Component
        {
            Transform existing = transform.Find(name);
            if (existing != null)
            {
                T c = existing.GetComponent<T>();
                if (c != null)
                    return c;
            }

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void BuildLookMaterials()
        {
            if (_renderers == null)
                return;
            _look = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                Color col = new Color(0.55f, 0.55f, 0.58f);
                float metallic = 0.8f;
                if (_kind == FieldLensTargetKind.Coil)
                    col = new Color(0.8f, 0.48f, 0.18f);
                else if (_kind == FieldLensTargetKind.Load)
                    col = new Color(0.32f, 0.22f, 0.14f);
                else if (_kind == FieldLensTargetKind.Cell)
                {
                    col = new Color(0.72f, 0.28f, 0.32f);
                    metallic = 0.08f;
                    if (r != null && r.gameObject.name == "Mitochondrion")
                        col = new Color(0.85f, 0.42f, 0.18f);
                    else if (r != null && r.gameObject.name == "Nucleus")
                        col = new Color(0.45f, 0.22f, 0.50f);
                }
                else if (r != null)
                {
                    string n = r.gameObject.name;
                    if (n == "North")
                        col = new Color(0.75f, 0.12f, 0.1f);
                    else if (n == "South")
                        col = new Color(0.12f, 0.22f, 0.7f);
                    else
                        col = new Color(0.28f, 0.3f, 0.33f);
                }

                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null)
                    s = Shader.Find("Standard");
                if (s == null)
                    s = Shader.Find("Sprites/Default");
                var mat = new Material(s)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    color = col
                };
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", col);
                if (mat.HasProperty("_Metallic"))
                    mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness"))
                    mat.SetFloat("_Smoothness", 0.72f);
                _look[i] = mat;
            }
        }

        void WireXri()
        {
            if (_xriWired)
                return;
            _xriWired = true;
            _grab = GetComponent<XRGrabInteractable>();
            _simple = GetComponent<XRSimpleInteractable>();
            if (_simple == null)
                _simple = GetComponentInChildren<XRSimpleInteractable>(true);
            if (_grab != null)
            {
                _grab.hoverEntered.AddListener(_ =>
                {
                    if (_lens != null)
                        _lens.Focus(this);
                });
                _grab.activated.AddListener(_ =>
                {
                    if (_lens != null)
                    {
                        _lens.Focus(this);
                        _lens.StepNext();
                    }
                });
            }

            if (_simple != null)
            {
                _simple.hoverEntered.AddListener(_ =>
                {
                    if (_lens != null)
                        _lens.Focus(this);
                });
                _simple.selectEntered.AddListener(_ =>
                {
                    if (_lens != null)
                    {
                        _lens.Focus(this);
                        _lens.StepNext();
                    }
                });
            }
        }

        public void SetScale(int scale)
        {
            _scale = Mathf.Clamp(scale, 0, ScaleEngine.StepCount - 1);
            ApplyLayer(_layer);
        }

        public void ApplyLayer(int layer)
        {
            _layer = Mathf.Clamp(layer, 0, FieldLens.LayerCount - 1);
            FieldLensLayer L = (FieldLensLayer)_layer;
            ScaleLevel S = (ScaleLevel)Mathf.Clamp(_scale, 0, ScaleEngine.StepCount - 1);

            if (_kind == FieldLensTargetKind.Cell)
            {
                MuscleCell cell = GetComponent<MuscleCell>();
                if (cell != null)
                    cell.ApplyView(_layer, _scale);
                ApplyMaterialLook(L == FieldLensLayer.Material || S == ScaleLevel.Material);
                UpdateHonesty();
                return;
            }

            // Field Lens Atomic hides solids; Scale Engine never hides the grabable magnet.
            bool showSolid = L != FieldLensLayer.Atomic;
            SetOriginalVisible(showSolid);
            ApplyMaterialLook(L == FieldLensLayer.Material || S == ScaleLevel.Material);

            if (_lattice != null)
                _lattice.Visible = L == FieldLensLayer.Atomic || S == ScaleLevel.Molecular;
            if (_charge != null)
                _charge.Visible = L == FieldLensLayer.Charge || S == ScaleLevel.Atomic;
            if (_electric != null)
                _electric.Visible = L == FieldLensLayer.Electric;
            if (_poynting != null)
                _poynting.Visible = L == FieldLensLayer.EnergyFlow;

            if (_magneticViz != null)
                _magneticViz.Visible = L == FieldLensLayer.Magnetic && _kind == FieldLensTargetKind.Magnet;
            if (_bSample != null)
                _bSample.Visible = L == FieldLensLayer.Magnetic && _kind == FieldLensTargetKind.Coil;

            UpdateHonesty();
        }

        void LateUpdate()
        {
            if (_bSample != null && _bSample.Visible)
                UpdateCoilBSample();
        }

        void UpdateCoilBSample()
        {
            InductionCoil coil = _circuit != null ? _circuit.Coil : GetComponent<InductionCoil>();
            if (coil == null || _dipole == null)
            {
                _bSample.HideAll();
                return;
            }

            Vector3 origin = coil.transform.position;
            float R = coil.Radius;
            int count = _bSample.Count;
            for (int k = 0; k < count; k++)
            {
                float a = (k / (float)count) * Mathf.PI * 2f;
                float r = R * 0.55f;
                Vector3 p = origin
                    + coil.transform.right * (Mathf.Cos(a) * r)
                    + coil.transform.forward * (Mathf.Sin(a) * r);
                Vector3 b = _dipole.CalculateFieldAt(p);
                if (b.sqrMagnitude < 1e-16f)
                {
                    _bSample.Hide(k);
                    continue;
                }

                _bSample.SetArrow(k, p, b, 0.035f);
            }
        }

        void SetOriginalVisible(bool visible)
        {
            if (_renderers == null)
                return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = visible;
            }
        }

        void ApplyMaterialLook(bool on)
        {
            if (_renderers == null)
                return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                    continue;
                if (on && _look != null && _look[i] != null)
                    _renderers[i].sharedMaterial = _look[i];
                else
                    _renderers[i].sharedMaterial = _original[i];
            }
        }

        void UpdateHonesty()
        {
            if (_honesty == null)
                return;
            string extra = "";
            FieldLensLayer L = (FieldLensLayer)_layer;
            if (L == FieldLensLayer.EnergyFlow)
                extra = "\nEducational approximation — not a full EM energy-flow solver";
            else if (L == FieldLensLayer.Atomic)
            {
                extra = "\nNot a literal quantum state";
                if (_kind == FieldLensTargetKind.Coil)
                    extra += "\n" + Element.Cu.Symbol + " Z=" + Element.Cu.Z + " shells " + Element.Cu.ElectronShells
                        + "\n" + Element.Cu.BondingOneLiner;
            }
            else if (L == FieldLensLayer.Charge)
            {
                extra = "\nNot a literal quantum state";
                if (_kind == FieldLensTargetKind.Coil)
                    extra += "\nCu coil current is classical I = EMF/R; charge glyphs are conceptual.";
            }
            else if (L == FieldLensLayer.Magnetic && _kind == FieldLensTargetKind.Coil)
                extra = "\nB sampled from MagneticDipole.CalculateFieldAt";
            else if (L == FieldLensLayer.Magnetic)
                extra = "\nB sampled from MagneticDipole.CalculateFieldAt";
            if (_kind == FieldLensTargetKind.Cell)
            {
                extra = "\n" + BioEnergy.Honesty;
                if (L == FieldLensLayer.Mathematical)
                    extra += "\nATP hydrolysis is conceptual chemical potential, not kinetics.";
                else if (L == FieldLensLayer.Charge || L == FieldLensLayer.EnergyFlow)
                    extra += "\n" + BioEnergy.Educational;
                else if (L == FieldLensLayer.Atomic)
                    extra += "\nMolecular schematic on the cell (not QM).";
                else if (L == FieldLensLayer.Electric || L == FieldLensLayer.Magnetic)
                    extra += "\nNot an EM source. Use the copper coil.";
            }

            _honesty.text = FieldLens.NameOf(_layer) + "\n" + FieldLens.HonestyOf(_layer) + extra;
        }

        void OnDestroy()
        {
            if (_look == null)
                return;
            for (int i = 0; i < _look.Length; i++)
            {
                if (_look[i] != null)
                    Destroy(_look[i]);
            }
        }
    }
}