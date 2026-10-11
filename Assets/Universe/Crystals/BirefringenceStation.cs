using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.Audio;
using RealityEngine.Player;
using C = RealityEngine.Crystals.CrystalConstants;

namespace RealityEngine.Crystals
{
    /// <summary>Linear polarizer sheet (toolbelt CRYSTAL > Polarizer, or the station's analyzer). Transmission axis = local +X.</summary>
    [DisallowMultipleComponent]
    public sealed class PolarizerSheet : MonoBehaviour, ICrystalInspectable, IDesktopActivatable
    {
        public const string RootName = "Gadget_Polarizer";
        public static readonly List<PolarizerSheet> All = new List<PolarizerSheet>();
        public bool Loose;
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public Vector3 Axis => transform.right;
        public Vector3 Normal => transform.up;

        public static GameObject Spawn(Vector3 pos)
        {
            var root = new GameObject(RootName);
            root.transform.position = pos;
            Build(root.transform, 0.14f);
            root.AddComponent<BoxCollider>().size = new Vector3(0.15f, 0.006f, 0.15f);
            var ps = root.AddComponent<PolarizerSheet>();
            ps.Loose = true;
            CrystalFx.MakeGrabbable(root, 0.05f);
            return root;
        }

        /// <summary>Grey disc + frame + an axis bar along local X.</summary>
        public static void Build(Transform parent, float diameter)
        {
            var film = CrystalFx.Crystal(new Color(0.18f, 0.2f, 0.22f, 0.55f));
            var frame = CrystalFx.Solid(new Color(0.08f, 0.09f, 0.1f), 0.3f, 0.5f);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Film", parent, Vector3.zero, new Vector3(diameter, 0.0015f, diameter), film, false);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Rim", parent, new Vector3(0f, -0.001f, 0f), new Vector3(diameter * 1.08f, 0.0008f, diameter * 1.08f), frame, false);
            CrystalFx.Prim(PrimitiveType.Cube, "AxisBar", parent, new Vector3(0f, 0.0018f, 0f), new Vector3(diameter * 0.95f, 0.0006f, 0.004f), CrystalFx.Emissive(new Color(0f, 0.9f, 1f), 1.2f), false);
            CrystalFx.Prim(PrimitiveType.Cube, "Tab", parent, new Vector3(diameter * 0.56f, 0f, 0f), new Vector3(diameter * 0.12f, 0.004f, 0.03f), frame, false);
        }

        public string InspectStats(bool held) => "Linear polarizer | cyan bar = transmission axis";
        public string InspectDetail() => "Malus: I = I0 cos^2(theta) | unpolarized light: I = I0 / 2";
        public string InspectHint(bool held) => held ? "Hold it flat over the calcite and twist" : "Grab and look through it";
        public void DesktopActivate(int delta) => transform.Rotate(0f, 15f * (delta < 0 ? -1f : 1f), 0f, Space.Self);
    }

    /// <summary>
    /// Station 4: double refraction in calcite. A dot under an Iceland-spar rhomb shows two images: the ordinary
    /// ray (n_o) goes straight; the extraordinary ray walks off toward the optic axis. They are polarised at 90 deg
    /// to each other, so an analyzer on top extinguishes one image at a time (Malus' law).
    /// Physics: walk-off tan(rho) = (n_o^2 - n_e^2) sin t cos t / (n_e^2 cos^2 t + n_o^2 sin^2 t), t = 44.6 deg
    /// (optic axis to the cleavage-face normal) -> rho ~ 6.2 deg; displacement s = thickness x tan(rho).
    /// Honesty: normal incidence, single wavelength (589 nm), no reflection losses, apparent-depth shift ignored.
    /// </summary>
    public sealed class BirefringenceStation : CrystalStation
    {
        public override string Id => "calcite";
        public override string Title => "Birefringence";
        public override string ChallengeId => "crystal_calcite";
        protected override CrystalUnitCell.Mode CellMode => CrystalUnitCell.Mode.Calcite;
        protected override string CellTitle => "calcite: CO3 layers + optic axis";

        public const double AxisTheta = 44.6;     // deg, optic axis vs the face normal for a cleavage rhomb
        const float Thick = 0.10f;                 // display specimen thickness (m)
        Transform _stage, _turn, _analyzer, _imgO, _imgE, _p2;
        Material _matO, _matE;
        CrystalDial _turnDial, _anaDial;
        bool _exaggerate, _analyzerIn = true, _p2In;
        float _alpha, _phi;
        bool _usingSheet;
        TextMeshPro _readout;
        LineRenderer _rayIn, _rayO, _rayE;
        Transform _diagram;
        float _tick;
        UnityEngine.UI.Button _exChip, _anaChip, _p2Chip;
        float _iO, _iE;

        public static double WalkOffRad(double thetaDeg)
        {
            double t = thetaDeg * Math.PI / 180.0;
            double no2 = C.CalciteNo * C.CalciteNo, ne2 = C.CalciteNe * C.CalciteNe;
            return Math.Atan((no2 - ne2) * Math.Sin(t) * Math.Cos(t) / (ne2 * Math.Cos(t) * Math.Cos(t) + no2 * Math.Sin(t) * Math.Sin(t)));
        }

        public static Mesh Parallelepiped(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 o = -(a + b + c) * 0.5f;
            Vector3[] p = { o, o + a, o + a + b, o + b, o + c, o + a + c, o + a + b + c, o + b + c };
            int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, new[] { 1, 2, 6, 5 }, new[] { 3, 0, 4, 7 } };
            var v = new List<Vector3>();
            var t = new List<int>();
            foreach (var f in faces)
            {
                int s = v.Count;
                for (int i = 0; i < 4; i++) v.Add(p[f[i]]);
                t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
            }
            var m = new Mesh { name = "Crystal_Rhomb" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            // Make sure the faces point outwards whatever the winding: flip if the first normal points inward.
            Vector3 n0 = Vector3.Cross(v[1] - v[0], v[2] - v[0]);
            Vector3 centre0 = (v[0] + v[1] + v[2] + v[3]) * 0.25f;
            if (Vector3.Dot(n0, centre0) < 0f)
            {
                var tri = m.triangles;
                for (int i = 0; i < tri.Length; i += 3) { int x = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = x; }
                m.triangles = tri;
                m.RecalculateNormals();
            }
            return m;
        }

        protected override void BuildStation()
        {
            Material dark = CrystalFx.Solid(new Color(0.12f, 0.13f, 0.15f), 0.6f, 0.5f);
            Material white = CrystalFx.Emissive(new Color(0.95f, 0.95f, 0.9f), 0.9f);

            // Tilted light-box stage so the top face looks at the player.
            _stage = new GameObject("Stage").transform;
            _stage.SetParent(transform, false);
            _stage.localPosition = OnTop(-0.25f, 0.16f, 0.08f);
            _stage.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            CrystalFx.Prim(PrimitiveType.Cube, "StandLeg", transform, OnTop(-0.25f, 0.07f, 0.14f), new Vector3(0.06f, 0.14f, 0.06f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "LightBox", _stage, new Vector3(0f, -0.02f, 0f), new Vector3(0.3f, 0.04f, 0.3f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "Diffuser", _stage, new Vector3(0f, 0.0005f, 0f), new Vector3(0.27f, 0.001f, 0.27f), white, false);
            CrystalFx.MeshObject("Dot", _stage, CrystalFx.LowSphere, CrystalFx.Solid(Color.black, 0f, 0.1f), new Vector3(0f, 0.0012f, 0f), Quaternion.identity, new Vector3(0.009f, 0.0006f, 0.009f));

            // Polarizer 2 (under the crystal, fixed at 0 deg) - optional.
            _p2 = new GameObject("Polarizer2").transform;
            _p2.SetParent(_stage, false);
            _p2.localPosition = new Vector3(0f, 0.004f, 0f);
            PolarizerSheet.Build(_p2, 0.2f);
            _p2.gameObject.SetActive(false);

            // Turntable + calcite rhomb.
            _turn = new GameObject("Turntable").transform;
            _turn.SetParent(_stage, false);
            _turn.localPosition = new Vector3(0f, 0.006f, 0f);
            float w = 0.11f;
            float lean = Thick * Mathf.Tan(25f * Mathf.Deg2Rad);
            var rhombMesh = Parallelepiped(new Vector3(w, 0f, 0f), new Vector3(w * 0.35f, 0f, w * 0.94f), new Vector3(lean * 0.7f, Thick, lean * 0.7f));
            var rhomb = CrystalFx.MeshObject("CalciteRhomb", _turn, rhombMesh, CrystalFx.Crystal(new Color(0.95f, 0.97f, 1f, 0.28f), new Color(0.03f, 0.04f, 0.05f)),
                new Vector3(0f, Thick * 0.5f, 0f), Quaternion.identity, Vector3.one);
            var rc = rhomb.AddComponent<BoxCollider>();
            rc.size = new Vector3(w * 1.2f, Thick, w * 1.1f);
            rhomb.AddComponent<XRSimpleInteractable>();
            rhomb.AddComponent<CalciteInspect>().Station = this;
            var ring = CrystalFx.Prim(PrimitiveType.Cylinder, "TurnRing", _turn, new Vector3(0f, -0.001f, 0f), new Vector3(0.24f, 0.003f, 0.24f), CrystalFx.Emissive(new Color(0f, 0.6f, 0.75f), 0.5f));
            CrystalFx.FlatCollider(ring);
            _turnDial = ring.AddComponent<CrystalDial>();
            _turnDial.Label = "Turn crystal";
            _turnDial.Wrap = true;
            _turnDial.TurnsForFull = 1.5f;
            _turnDial.DesktopStep = 1f / 24f;
            // Optic-axis marker on top of the crystal (projection direction = local +X of the turntable).
            CrystalFx.Prim(PrimitiveType.Cube, "AxisMark", _turn, new Vector3(0.07f, Thick + 0.001f, 0f), new Vector3(0.05f, 0.001f, 0.004f), CrystalFx.Emissive(new Color(1f, 0.7f, 0.2f), 1.2f), false);

            // Images (seen at the top face).
            _matO = new Material(CrystalFx.Crystal(new Color(0f, 0f, 0f, 0.95f))) { name = "Crystal_ImageO" };
            _matE = new Material(CrystalFx.Crystal(new Color(0f, 0f, 0f, 0.951f))) { name = "Crystal_ImageE" };
            _imgO = CrystalFx.MeshObject("ImageO", _stage, CrystalFx.LowSphere, _matO, new Vector3(0f, 0.006f + Thick + 0.0015f, 0f), Quaternion.identity, new Vector3(0.009f, 0.0005f, 0.009f)).transform;
            _imgE = CrystalFx.MeshObject("ImageE", _stage, CrystalFx.LowSphere, _matE, new Vector3(0f, 0.006f + Thick + 0.0015f, 0f), Quaternion.identity, new Vector3(0.009f, 0.0005f, 0.009f)).transform;

            // Analyzer above.
            _analyzer = new GameObject("Analyzer").transform;
            _analyzer.SetParent(_stage, false);
            _analyzer.localPosition = new Vector3(0f, 0.006f + Thick + 0.035f, 0f);
            PolarizerSheet.Build(_analyzer, 0.22f);
            var grip = CrystalFx.Prim(PrimitiveType.Cylinder, "AnalyzerRing", _analyzer, new Vector3(0f, 0f, 0f), new Vector3(0.24f, 0.003f, 0.24f), CrystalFx.Emissive(new Color(0.6f, 0.3f, 1f), 0.4f));
            CrystalFx.FlatCollider(grip);
            _anaDial = grip.AddComponent<CrystalDial>();
            _anaDial.Label = "Turn polarizer";
            _anaDial.Wrap = true;
            _anaDial.TurnsForFull = 1.5f;
            _anaDial.DesktopStep = 1f / 24f;
            CrystalFx.Text(transform, "StageLabel", "turn the TEAL ring = crystal     turn the VIOLET ring = polarizer", 0.13f, new Color(0.6f, 0.9f, 1f, 0.9f), OnTop(-0.25f, 0.02f, -0.22f), new Vector2(0.75f, 0.04f));

            // Ray diagram (right): cross-section, O straight, E walks off by rho.
            _diagram = new GameObject("RayDiagram").transform;
            _diagram.SetParent(transform, false);
            _diagram.localPosition = OnTop(0.48f, 0.18f, 0.12f);
            CrystalFx.Prim(PrimitiveType.Cube, "DiagramPanel", _diagram, new Vector3(0f, 0f, 0.012f), new Vector3(0.44f, 0.34f, 0.02f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "Slab", _diagram, new Vector3(0f, -0.02f, -0.001f), new Vector3(0.3f, 0.14f, 0.001f), CrystalFx.Crystal(new Color(0.7f, 0.9f, 1f, 0.25f)), false);
            _rayIn = Ray("RayIn", new Color(1f, 1f, 0.85f));
            _rayO = Ray("RayO", new Color(1f, 0.4f, 0.35f));
            _rayE = Ray("RayE", new Color(0.35f, 0.7f, 1f));
            CrystalFx.Text(_diagram, "DiagLabel", "<color=#FF6A5A>O ray</color> straight      <color=#6FA8FF>E ray</color> walks off", 0.13f, Color.white, new Vector3(0f, 0.14f, -0.003f), new Vector2(0.42f, 0.04f));
            _readout = CrystalFx.Text(_diagram, "Readout", "", 0.13f, new Color(0.55f, 1f, 0.85f), new Vector3(0f, -0.13f, -0.003f), new Vector2(0.42f, 0.07f));

            _turnDial.Set(0.125f);
        }

        LineRenderer Ray(string name, Color c)
        {
            var g = new GameObject(name);
            g.transform.SetParent(_diagram, false);
            var lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.widthMultiplier = 0.004f;
            lr.sharedMaterial = CrystalFx.Line(c);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        public float ShiftM => (float)(Thick * Math.Tan(WalkOffRad(AxisTheta))) * (_exaggerate ? 3f : 1f);
        public float IntensityO => _iO;
        public float IntensityE => _iE;
        public float CrystalAngle => _alpha;
        public float AnalyzerAngle => _phi;

        void Update()
        {
            if (_turn == null)
                return;
            _alpha = _turnDial.Value01 * 360f;
            _turn.localRotation = Quaternion.Euler(0f, -_alpha, 0f);

            // Analyzer angle: the station ring, or a loose Polarizer gadget held flat over the crystal.
            _usingSheet = false;
            _phi = _anaDial.Value01 * 360f;
            Vector3 top = _stage.TransformPoint(new Vector3(0f, Thick + 0.04f, 0f));
            for (int i = 0; i < PolarizerSheet.All.Count; i++)
            {
                PolarizerSheet s = PolarizerSheet.All[i];
                if (s == null || !s.Loose) continue;
                if ((s.transform.position - top).sqrMagnitude > 0.2f * 0.2f) continue;
                if (Mathf.Abs(Vector3.Dot(s.Normal, _stage.up)) < 0.8f) continue;
                Vector3 ax = Vector3.ProjectOnPlane(s.Axis, _stage.up);
                if (ax.sqrMagnitude < 1e-4f) continue;
                _phi = Vector3.SignedAngle(_stage.right, ax, -_stage.up);
                _usingSheet = true;
                break;
            }
            _analyzer.gameObject.SetActive(_analyzerIn && !_usingSheet);
            _analyzer.localRotation = Quaternion.Euler(0f, -_anaDial.Value01 * 360f, 0f);
            bool analyzing = _analyzerIn || _usingSheet;

            // E image displaced toward the optic-axis projection; O image stays over the dot.
            Vector3 dir = Quaternion.Euler(0f, -_alpha, 0f) * Vector3.right;
            float y = 0.006f + Thick + 0.0015f;
            _imgO.localPosition = new Vector3(0f, y, 0f);
            _imgE.localPosition = new Vector3(0f, y, 0f) + dir * ShiftM;

            // Intensities. E vibrates in the principal plane (angle alpha); O perpendicular. Unpolarized source gives 1/2 each.
            float d = (_phi - _alpha) * Mathf.Deg2Rad;
            float eIn = 0.5f, oIn = 0.5f;
            if (_p2In)
            {
                float a = _alpha * Mathf.Deg2Rad;
                eIn = 0.5f * Mathf.Cos(a) * Mathf.Cos(a);
                oIn = 0.5f * Mathf.Sin(a) * Mathf.Sin(a);
            }
            _iE = analyzing ? eIn * Mathf.Cos(d) * Mathf.Cos(d) : eIn;
            _iO = analyzing ? oIn * Mathf.Sin(d) * Mathf.Sin(d) : oIn;
            SetAlpha(_matE, _iE * 2f);
            SetAlpha(_matO, _iO * 2f);

            if (analyzing && !_p2In)
            {
                float mx = Mathf.Max(_iO, _iE), mn = Mathf.Min(_iO, _iE);
                if (mx > 0.2f && mn / mx < 0.02f)
                    CrystalGoals.Latch("calcite_extinct");
            }
            if (_p2In)
            {
                float total = _iO + _iE;
                if (total < 0.01f) CrystalGoals.Latch("calcite_dark");
            }

            // Ray diagram.
            float rho = (float)WalkOffRad(AxisTheta) * (_exaggerate ? 3f : 1f);
            float h = 0.14f, cy = -0.02f;
            _rayIn.SetPosition(0, new Vector3(-0.04f, 0.11f, -0.003f)); _rayIn.SetPosition(1, new Vector3(-0.04f, cy + h * 0.5f, -0.003f));
            _rayO.SetPosition(0, new Vector3(-0.04f, cy + h * 0.5f, -0.003f)); _rayO.SetPosition(1, new Vector3(-0.04f, cy - h * 0.5f - 0.04f, -0.003f));
            float off = h * Mathf.Tan(rho);
            _rayE.SetPosition(0, new Vector3(-0.04f, cy + h * 0.5f, -0.004f)); _rayE.SetPosition(1, new Vector3(-0.04f + off, cy - h * 0.5f, -0.004f));
            if (_rayE.positionCount != 3) _rayE.positionCount = 3;
            _rayE.SetPosition(2, new Vector3(-0.04f + off, cy - h * 0.5f - 0.04f, -0.004f));

            if (Cell != null)
            {
                Cell.Drive = Mathf.Repeat(_alpha / 180f + 1f, 2f) - 1f;
                Cell.SetCaption("light vibrating along the CO3\nplanes is slowed more (n_o)");
            }

            _tick += Time.deltaTime;
            if (_tick > 0.15f && _readout != null)
            {
                _tick = 0f;
                _readout.text = "rho = " + (rho * Mathf.Rad2Deg).ToString("0.0") + " deg   shift " + (ShiftM * 1000f).ToString("0.0") + " mm" + (_exaggerate ? " (x3)" : "")
                    + "\ncrystal " + _alpha.ToString("0") + " deg  polarizer " + Mathf.Repeat(_phi, 360f).ToString("0") + " deg" + (_usingSheet ? " (your sheet)" : "")
                    + "\nO " + (_iO * 200f).ToString("0") + "%   E " + (_iE * 200f).ToString("0") + "%";
            }
        }

        static void SetAlpha(Material m, float a)
        {
            Color c = new Color(0f, 0f, 0f, Mathf.Clamp01(a) * 0.95f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
        }

        protected override CrystalGuideCard.Content GetContent()
        {
            return new CrystalGuideCard.Content
            {
                Title = "4  BIREFRINGENCE  - one dot, two images",
                ChallengeId = ChallengeId,
                What =
                    "Look down through the clear calcite (Iceland spar): the single black dot appears TWICE.\n\n"
                    + "In calcite, light travels at different speeds depending on which way it vibrates. Unpolarised light splits into two beams: "
                    + "the ordinary ray goes straight through; the extraordinary ray is pushed sideways toward the crystal's optic axis. "
                    + "Turn the crystal and the second image orbits the first.\n\n"
                    + "The two beams are polarised at right angles to each other. Put a polarizer on top and you can switch one image off, then the other.\n\n"
                    + "Vikings may have used such 'sunstones' to find the sun through cloud.\n\n"
                    + "<color=#9FB4C4>Model: normal incidence, 589 nm, no reflection losses; image depth effects ignored. 10 cm display specimen.</color>",
                Math =
                    "<b>Indices (589 nm)</b>   n_o = 1.658,  n_e = 1.486   (birefringence 0.172)\n"
                    + "<b>Walk-off angle</b>\n   tan(rho) = (n_o^2 - n_e^2) sin t cos t / (n_e^2 cos^2 t + n_o^2 sin^2 t)\n"
                    + "   t = 44.6 deg (optic axis to the face normal) -> rho = 6.2 deg\n"
                    + "<b>Image shift</b>   s = thickness x tan(rho) = 100 mm x 0.109 = 10.9 mm\n"
                    + "<b>Malus' law</b>   I = I0 cos^2(phi - alpha)   for E;   I0 sin^2(phi - alpha)   for O\n"
                    + "<b>Unpolarised in</b>   each ray carries half\n\n"
                    + "[Hecht, Optics 5e, ch. 8; Born & Wolf; Ghosh 1999 (calcite dispersion), Opt. Commun. 163]",
                TryThis =
                    "1. Look down at the dot through the crystal (step close - it is on the tilted light box). Two dots!\n"
                    + "2. Twist the TEAL ring: one image stays put (O), the other circles round it (E). The orange bar marks the optic-axis direction.\n"
                    + "3. Twist the VIOLET ring (polarizer). At some angle one dot vanishes completely. 90 deg later the other one does.\n"
                    + "4. CHALLENGE: find an extinction angle (one image < 2% of the other).\n"
                    + "5. Toolbelt CRYSTAL > Polarizer: hold your own sheet flat over the crystal and twist it.\n"
                    + "6. 'Polarizer 2' adds a sheet under the crystal: now you can make BOTH images go dark.",
                Lattice =
                    "Calcite is CaCO3. The flat carbonate triangles (CO3) all lie in parallel planes, stacked along one direction: the optic axis (arrow in the model).\n\n"
                    + "Light whose electric field vibrates IN those planes pushes the electron clouds of the flat CO3 groups easily, so it is slowed more (n_o = 1.658). "
                    + "Light with a field component along the axis feels less of this (n_e down to 1.486).\n\n"
                    + "Because the 'slowness' depends on direction, the energy of the E ray flows at an angle to its wavefront: that is the walk-off you see."
            };
        }

        protected override void AddControls(CrystalGuideCard card)
        {
            card.AddControl("Crystal +15", () => _turnDial.Set(_turnDial.Value01 + 15f / 360f));
            card.AddControl("Crystal -15", () => _turnDial.Set(_turnDial.Value01 - 15f / 360f));
            card.AddControl("Polar. +5", () => _anaDial.Set(_anaDial.Value01 + 5f / 360f));
            card.AddControl("Polar. -5", () => _anaDial.Set(_anaDial.Value01 - 5f / 360f));
            card.AddControl("Polar. +45", () => _anaDial.Set(_anaDial.Value01 + 45f / 360f));
            _anaChip = card.AddControl("Analyzer: IN", () =>
            {
                _analyzerIn = !_analyzerIn;
                CrystalGuideCard.SetChipLabel(_anaChip, _analyzerIn ? "Analyzer: IN" : "Analyzer: OUT");
            });
            _exChip = card.AddControl("Exaggerate x3", () =>
            {
                _exaggerate = !_exaggerate;
                CrystalGuideCard.SetChipLabel(_exChip, _exaggerate ? "Real scale" : "Exaggerate x3");
            });
            _p2Chip = card.AddControl("Polarizer 2: OFF", () =>
            {
                _p2In = !_p2In;
                _p2.gameObject.SetActive(_p2In);
                CrystalGuideCard.SetChipLabel(_p2Chip, _p2In ? "Polarizer 2: ON" : "Polarizer 2: OFF");
            });
        }

        protected override string LiveLine()
        {
            return "walk-off rho " + (WalkOffRad(AxisTheta) * 180.0 / Math.PI).ToString("0.00") + " deg   shift " + (ShiftM * 1000f).ToString("0.0") + " mm"
                + "   crystal alpha " + _alpha.ToString("0") + "   polarizer phi " + Mathf.Repeat(_phi, 360f).ToString("0") + "   phi - alpha "
                + Mathf.Repeat(_phi - _alpha, 180f).ToString("0") + " deg\nI_O = " + (_iO * 2f).ToString("0.00") + " x I0/2   I_E = " + (_iE * 2f).ToString("0.00") + " x I0/2"
                + (_p2In ? "   (polarizer 2 at 0 deg underneath)" : "");
        }
    }

    /// <summary>Inspect-strip readout for the calcite rhomb.</summary>
    public sealed class CalciteInspect : MonoBehaviour, ICrystalInspectable
    {
        public BirefringenceStation Station;
        public string InspectStats(bool held) => "Calcite (Iceland spar) | n_o 1.658 n_e 1.486 | O " + (Station.IntensityO * 200f).ToString("0") + "% E " + (Station.IntensityE * 200f).ToString("0") + "%";
        public string InspectDetail() => "walk-off 6.2 deg | image shift " + (Station.ShiftM * 1000f).ToString("0.0") + " mm";
        public string InspectHint(bool held) => "Turn the teal ring (crystal) or violet ring (polarizer)";
    }
}
