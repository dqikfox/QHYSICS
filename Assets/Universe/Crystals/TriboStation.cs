using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.Audio;
using RealityEngine.Player;

namespace RealityEngine.Crystals
{
    /// <summary>
    /// Something that glows when cracked or struck. Fracture separates charge across the crack; the gap
    /// discharges through the air and excites N2 (UV lines at 337 / 357 / 380 nm). Wintergreen (methyl salicylate)
    /// absorbs that UV and re-emits blue (~450 nm), which is why wintergreen mints flash so visibly.
    /// Quartz pebbles struck together glow orange (local heating / surface excitation at the contact).
    /// Honesty: brightness is a perceptual scale of 1/2 m v^2, not a photon count; thresholds are tuned (0.8 m/s).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TriboCandy : MonoBehaviour, ICrystalInspectable, IDesktopActivatable
    {
        public enum Kind { Wintergreen, Sugar, Quartz }
        public static readonly List<TriboCandy> All = new List<TriboCandy>();
        public static event Action<TriboCandy, float> Flashed;
        public const float BreakSpeed = 0.8f;
        public Kind Type;
        public int Generation;
        public float LastEnergyJ { get; private set; }
        Rigidbody _rb;
        XRGrabInteractable _grab;
        float _cool;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _grab = GetComponent<XRGrabInteractable>();
        }

        public static string KindName(Kind k) => k == Kind.Wintergreen ? "wintergreen mint" : (k == Kind.Sugar ? "sugar cube" : "quartz pebble");
        public static float MassKg(Kind k) => k == Kind.Wintergreen ? 0.0025f : (k == Kind.Sugar ? 0.004f : 0.015f);

        public static Color GlowColor(Kind k)
        {
            switch (k)
            {
                case Kind.Wintergreen: return new Color(0.35f, 0.6f, 1f);
                case Kind.Sugar: return new Color(0.55f, 0.5f, 1f);
                default: return new Color(1f, 0.55f, 0.15f);
            }
        }

        /// <summary>Relative brightness per wavelength band 337, 380, 420, 450, 500, 600 nm.</summary>
        public static float[] Spectrum(Kind k)
        {
            switch (k)
            {
                case Kind.Wintergreen: return new[] { 0.35f, 0.15f, 0.55f, 1f, 0.45f, 0.08f };
                case Kind.Sugar: return new[] { 1f, 0.55f, 0.15f, 0.05f, 0.02f, 0f };
                default: return new[] { 0f, 0f, 0.05f, 0.1f, 0.3f, 1f };
            }
        }

        public static GameObject Create(Kind k, Vector3 pos, string name, int generation = 0, float scale = 1f)
        {
            var root = new GameObject(name);
            root.transform.position = pos;
            switch (k)
            {
                case Kind.Wintergreen:
                    CrystalFx.Prim(PrimitiveType.Cylinder, "Mint", root.transform, Vector3.zero, new Vector3(0.019f, 0.0035f, 0.019f) * scale, CrystalFx.Solid(new Color(0.97f, 0.97f, 0.95f), 0f, 0.35f), false);
                    CrystalFx.Prim(PrimitiveType.Cylinder, "Hole", root.transform, Vector3.zero, new Vector3(0.006f, 0.0037f, 0.006f) * scale, CrystalFx.Solid(new Color(0.75f, 0.85f, 0.8f), 0f, 0.3f), false);
                    root.AddComponent<BoxCollider>().size = new Vector3(0.019f, 0.007f, 0.019f) * scale;
                    break;
                case Kind.Sugar:
                    CrystalFx.Prim(PrimitiveType.Cube, "Cube", root.transform, Vector3.zero, Vector3.one * 0.015f * scale, CrystalFx.Solid(new Color(0.98f, 0.98f, 0.97f), 0f, 0.15f), false);
                    root.AddComponent<BoxCollider>().size = Vector3.one * 0.015f * scale;
                    break;
                default:
                    CrystalFx.MeshObject("Pebble", root.transform, CrystalFx.LowSphere, CrystalFx.Crystal(new Color(0.95f, 0.92f, 0.88f, 0.8f), new Color(0.04f, 0.03f, 0.02f)),
                        Vector3.zero, Quaternion.Euler(0f, 30f, 10f), new Vector3(0.03f, 0.022f, 0.026f) * scale);
                    root.AddComponent<SphereCollider>().radius = 0.013f * scale;
                    break;
            }
            var c = root.AddComponent<TriboCandy>();
            c.Type = k;
            c.Generation = generation;
            float m = MassKg(k) * scale * scale * scale;
            if (generation == 0)
            {
                CrystalFx.MakeGrabbable(root, Mathf.Max(0.001f, m));
                c._grab = root.GetComponent<XRGrabInteractable>();
            }
            else
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(0.0003f, m);
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
            c._rb = root.GetComponent<Rigidbody>();
            return root;
        }

        void Update()
        {
            if (_cool > 0f) _cool -= Time.deltaTime;
        }

        void OnCollisionEnter(Collision col)
        {
            if (col == null || col.contactCount == 0 || _cool > 0f)
                return;
            float vn = Mathf.Abs(Vector3.Dot(col.relativeVelocity, col.GetContact(0).normal));
            if (vn < BreakSpeed)
                return;
            if (Type == Kind.Quartz)
            {
                var other = col.collider.GetComponentInParent<TriboCandy>();
                bool hard = other != null || col.collider.GetComponentInParent<Rigidbody>() == null || col.rigidbody == null || col.rigidbody.mass > 0.2f;
                if (!hard)
                    return;
            }
            Fracture(vn, col.GetContact(0).point);
        }

        /// <summary>Crack (or for quartz: strike) at impact speed v.</summary>
        public void Fracture(float v, Vector3 at)
        {
            if (_cool > 0f)
                return;
            _cool = 0.25f;
            float m = _rb != null ? _rb.mass : MassKg(Type);
            float e = 0.5f * m * v * v;
            LastEnergyJ = e;
            // Perceptual brightness: log of energy, x kind efficiency (wintergreen > quartz > sugar).
            float eff = Type == Kind.Wintergreen ? 1f : (Type == Kind.Quartz ? 0.75f : 0.4f);
            float b = Mathf.Clamp01((Mathf.Log10(Mathf.Max(1e-6f, e)) + 4.5f) / 3f) * eff;
            CrystalFx.Flash(at, GlowColor(Type), 0.4f + 2.6f * b, Type == Kind.Quartz ? 0.28f : 0.12f, 0.02f + 0.03f * b);
            QhysicsSfx.PlayAt(Type == Kind.Quartz ? SfxId.CrystalClick : SfxId.TriboCrackle, at, 0.35f + 0.4f * b, Type == Kind.Sugar ? 1.3f : 1f);
            CrystalFx.PulseHolders(_grab, 0.2f + 0.5f * b, 0.04f);
            if (Flashed != null)
                Flashed(this, b);
            if (Type == Kind.Wintergreen && b > 0.25f)
                CrystalGoals.Latch("tribo_wintergreen");
            if (Type == Kind.Sugar && b > 0.1f)
                CrystalGoals.Latch("tribo_sugar");
            if (Type == Kind.Quartz)
            {
                CrystalGoals.Latch("tribo_quartz");
                return;
            }
            if (Generation >= 1)
            {
                Destroy(gameObject);
                return;
            }
            // Split into three fragments that keep the body's motion.
            Vector3 vel = _rb != null ? _rb.linearVelocity : Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                Vector3 off = Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0.006f, 0.004f, 0f);
                var frag = Create(Type, transform.position + off, "Crystal_Fragment", 1, 0.55f);
                var rb = frag.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = vel * 0.3f + off.normalized * 0.4f;
                Destroy(frag, 20f);
            }
            var home = GetComponent<CrystalHomeReturn>();
            if (home != null)
                home.enabled = false;
            if (_grab != null && _grab.isSelected && _grab.interactionManager != null)
            {
                var sel = _grab.firstInteractorSelecting;
                if (sel != null)
                    _grab.interactionManager.SelectExit(sel, _grab);
            }
            Destroy(gameObject);
        }

        public string InspectStats(bool held) => KindName(Type) + " | breaks above " + BreakSpeed.ToString("0.0") + " m/s | last 1/2mv^2 " + (LastEnergyJ * 1000f).ToString("0.00") + " mJ";
        public string InspectDetail() => Type == Kind.Wintergreen ? "N2 UV 337 nm -> methyl salicylate fluoresces blue ~450 nm"
            : (Type == Kind.Sugar ? "N2 discharge only: faint violet / UV" : "struck quartz glows orange at the contact");
        public string InspectHint(bool held) => held ? "Smash it on something hard (LMB = crunch)" : "Grab it - best in the dark hood";
        public void DesktopActivate(int delta) => Fracture(1.6f, transform.position);
    }

    /// <summary>Station 5: triboluminescence in a dark hood, with an anvil, a drop pestle and a spectrum readout.</summary>
    public sealed class TriboStation : CrystalStation
    {
        public override string Id => "tribo";
        public override string Title => "Triboluminescence";
        public override string ChallengeId => "crystal_tribo";
        protected override CrystalUnitCell.Mode CellMode => CrystalUnitCell.Mode.Tribo;
        protected override string CellTitle => "crack: + and - faces pull apart";

        Transform _hood, _anvilTop, _pestle, _tray, _hoodDoor;
        readonly List<Transform> _bars = new List<Transform>();
        TextMeshPro _spectrumTitle;
        float _crack, _pestleT = -1f, _barHold;
        float[] _spec = new float[6];
        string _lastKind = "-";
        float _lastB;
        bool _door = true;
        UnityEngine.UI.Button _doorChip;
        readonly List<GameObject> _stock = new List<GameObject>();

        protected override void BuildStation()
        {
            Material black = CrystalFx.Line(new Color(0.01f, 0.01f, 0.015f, 1f));
            Material dark = CrystalFx.Solid(new Color(0.12f, 0.13f, 0.15f), 0.6f, 0.5f);
            Material steel = CrystalFx.Solid(new Color(0.55f, 0.58f, 0.62f), 0.85f, 0.55f);

            // Dark hood: open-front box (unlit black inside) so flashes stand out even in a lit lab.
            _hood = new GameObject("DarkHood").transform;
            _hood.SetParent(transform, false);
            _hood.localPosition = OnTop(-0.2f, 0f, 0.05f);
            CrystalFx.Prim(PrimitiveType.Cube, "Floor", _hood, new Vector3(0f, 0.004f, 0f), new Vector3(0.62f, 0.008f, 0.44f), black);
            CrystalFx.Prim(PrimitiveType.Cube, "Back", _hood, new Vector3(0f, 0.2f, 0.22f), new Vector3(0.62f, 0.4f, 0.01f), black);
            CrystalFx.Prim(PrimitiveType.Cube, "Left", _hood, new Vector3(-0.31f, 0.2f, 0f), new Vector3(0.01f, 0.4f, 0.44f), black);
            CrystalFx.Prim(PrimitiveType.Cube, "Right", _hood, new Vector3(0.31f, 0.2f, 0f), new Vector3(0.01f, 0.4f, 0.44f), black);
            CrystalFx.Prim(PrimitiveType.Cube, "Roof", _hood, new Vector3(0f, 0.4f, 0f), new Vector3(0.62f, 0.01f, 0.44f), black);
            var door = CrystalFx.Prim(PrimitiveType.Cube, "Visor", _hood, new Vector3(0f, 0.31f, -0.22f), new Vector3(0.62f, 0.18f, 0.008f), black, false);
            _hoodDoor = door.transform;
            CrystalFx.Text(_hood, "HoodLabel", "DARK HOOD - reach in", 0.16f, new Color(0f, 0.9f, 1f, 0.9f), new Vector3(0f, 0.43f, -0.22f), new Vector2(0.6f, 0.04f));

            // Anvil + drop pestle on a rail.
            CrystalFx.Prim(PrimitiveType.Cube, "Anvil", _hood, new Vector3(0.05f, 0.03f, 0.04f), new Vector3(0.1f, 0.05f, 0.08f), steel);
            _anvilTop = new GameObject("AnvilTop").transform;
            _anvilTop.SetParent(_hood, false);
            _anvilTop.localPosition = new Vector3(0.05f, 0.056f, 0.04f);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Rail", _hood, new Vector3(0.05f, 0.25f, 0.12f), new Vector3(0.012f, 0.15f, 0.012f), dark, false);
            CrystalFx.Prim(PrimitiveType.Cube, "RailArm", _hood, new Vector3(0.05f, 0.395f, 0.08f), new Vector3(0.02f, 0.01f, 0.09f), dark, false);
            var pestle = CrystalFx.Prim(PrimitiveType.Cylinder, "Pestle", _hood, new Vector3(0.05f, 0.3f, 0.04f), new Vector3(0.035f, 0.05f, 0.035f), steel, false);
            _pestle = pestle.transform;
            var pt = CrystalFx.Prim(PrimitiveType.Cube, "CrushButton", transform, OnTop(0.2f, 0.02f, -0.2f), new Vector3(0.06f, 0.03f, 0.05f), CrystalFx.Solid(new Color(0.9f, 0.2f, 0.15f))).AddComponent<CrystalToggle>();
            pt.Label = "Drop pestle";
            pt.Toggled = DropPestle;
            CrystalFx.Text(transform, "CrushLabel", "DROP PESTLE", 0.13f, new Color(1f, 0.6f, 0.5f), OnTop(0.2f, 0.05f, -0.2f), new Vector2(0.2f, 0.03f));

            // Tray (outside the hood, front-left).
            _tray = new GameObject("CandyTray").transform;
            _tray.SetParent(transform, false);
            _tray.localPosition = OnTop(-0.2f, 0f, -0.25f);
            CrystalFx.Prim(PrimitiveType.Cube, "Tray", _tray, new Vector3(0f, 0.004f, 0f), new Vector3(0.36f, 0.008f, 0.12f), dark);
            Restock();

            // Spectrum panel (right).
            var sp = new GameObject("Spectrum").transform;
            sp.SetParent(transform, false);
            sp.localPosition = OnTop(0.55f, 0.18f, 0.12f);
            CrystalFx.Prim(PrimitiveType.Cube, "SpecPanel", sp, new Vector3(0f, 0f, 0.012f), new Vector3(0.44f, 0.32f, 0.02f), dark);
            string[] nm = { "337", "380", "420", "450", "500", "600" };
            Color[] cols = { new Color(0.55f, 0.3f, 0.9f), new Color(0.45f, 0.3f, 1f), new Color(0.3f, 0.35f, 1f), new Color(0.2f, 0.55f, 1f), new Color(0.2f, 1f, 0.5f), new Color(1f, 0.55f, 0.1f) };
            for (int i = 0; i < 6; i++)
            {
                float x = -0.17f + i * 0.068f;
                var bar = CrystalFx.Prim(PrimitiveType.Cube, "Bar" + nm[i], sp, new Vector3(x, -0.09f, -0.002f), new Vector3(0.045f, 0.001f, 0.004f), CrystalFx.Emissive(cols[i], 1.3f), false);
                _bars.Add(bar.transform);
                CrystalFx.Text(sp, "Nm" + nm[i], nm[i], 0.11f, new Color(0.7f, 0.8f, 0.9f), new Vector3(x, -0.115f, -0.003f), new Vector2(0.07f, 0.03f));
            }
            _spectrumTitle = CrystalFx.Text(sp, "SpecTitle", "spectrum of last flash (nm)", 0.12f, new Color(0.6f, 0.9f, 1f), new Vector3(0f, 0.12f, -0.003f), new Vector2(0.42f, 0.06f));
            TriboCandy.Flashed += OnFlash;
        }

        void OnDestroy()
        {
            TriboCandy.Flashed -= OnFlash;
        }

        void Restock()
        {
            for (int i = 0; i < _stock.Count; i++)
                if (_stock[i] != null) Destroy(_stock[i]);
            _stock.Clear();
            var kinds = new[] { TriboCandy.Kind.Wintergreen, TriboCandy.Kind.Wintergreen, TriboCandy.Kind.Wintergreen, TriboCandy.Kind.Sugar, TriboCandy.Kind.Sugar, TriboCandy.Kind.Quartz, TriboCandy.Kind.Quartz };
            for (int i = 0; i < kinds.Length; i++)
            {
                Vector3 p = _tray.TransformPoint(new Vector3(-0.15f + i * 0.05f, 0.025f, 0f));
                var g = TriboCandy.Create(kinds[i], p, "Crystal_Candy_" + kinds[i] + "_" + i);
                _stock.Add(g);
            }
            // One extra wintergreen pre-placed on the anvil for the pestle.
            _stock.Add(TriboCandy.Create(TriboCandy.Kind.Wintergreen, _anvilTop.position + Vector3.up * 0.01f, "Crystal_Candy_Anvil"));
        }

        void DropPestle()
        {
            if (_pestleT >= 0f)
                return;
            _pestleT = 0f;
        }

        void OnFlash(TriboCandy c, float b)
        {
            if (c == null)
                return;
            float[] s = TriboCandy.Spectrum(c.Type);
            for (int i = 0; i < 6; i++) _spec[i] = s[i] * Mathf.Max(0.15f, b);
            _barHold = 2.5f;
            _crack = 1f;
            _lastKind = TriboCandy.KindName(c.Type);
            _lastB = b;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_pestleT >= 0f)
            {
                _pestleT += dt;
                float y;
                if (_pestleT < 0.18f)
                {
                    // free fall over 0.16 m: v = sqrt(2 g h) ~ 1.8 m/s
                    float k = _pestleT / 0.18f;
                    y = Mathf.Lerp(0.3f, 0.106f, k * k);
                }
                else
                {
                    if (_pestleT - dt < 0.18f)
                    {
                        QhysicsSfx.PlayAt(SfxId.HitBlunt, _anvilTop.position, 0.4f, 1.4f);
                        TriboCandy best = null;
                        float bd = 0.05f * 0.05f;
                        for (int i = 0; i < TriboCandy.All.Count; i++)
                        {
                            var c = TriboCandy.All[i];
                            if (c == null) continue;
                            float d = (c.transform.position - _anvilTop.position).sqrMagnitude;
                            if (d < bd) { bd = d; best = c; }
                        }
                        if (best != null)
                            best.Fracture(Mathf.Sqrt(2f * 9.81f * 0.16f), best.transform.position);
                    }
                    y = Mathf.Lerp(0.106f, 0.3f, Mathf.Clamp01((_pestleT - 0.5f) / 0.8f));
                    if (_pestleT > 1.35f) _pestleT = -1f;
                }
                _pestle.localPosition = new Vector3(0.05f, y, 0.04f);
            }

            if (_barHold > 0f) _barHold -= dt;
            for (int i = 0; i < 6; i++)
            {
                if (_barHold <= 0f) _spec[i] = Mathf.MoveTowards(_spec[i], 0f, dt * 0.4f);
                float h = Mathf.Max(0.001f, _spec[i] * 0.2f);
                _bars[i].localScale = new Vector3(0.045f, h, 0.004f);
                _bars[i].localPosition = new Vector3(_bars[i].localPosition.x, -0.1f + h * 0.5f, -0.002f);
            }
            if (_spectrumTitle != null)
                _spectrumTitle.text = "spectrum of last flash (nm)\n" + _lastKind + "  brightness " + (_lastB * 100f).ToString("0") + "%";

            _crack = Mathf.MoveTowards(_crack, 0f, dt * 0.6f);
            if (Cell != null)
            {
                Cell.Aux = _crack;
                Cell.SetCaption(_crack > 0.05f ? "charged crack faces ->\nspark in the gap (N2 glows UV)" : "intact crystal:\ncharges balanced");
            }
        }

        protected override CrystalGuideCard.Content GetContent()
        {
            return new CrystalGuideCard.Content
            {
                Title = "5  TRIBOLUMINESCENCE  - light from breaking crystals",
                ChallengeId = ChallengeId,
                What =
                    "Crunch a wintergreen mint in a dark room and you see a blue flash. Crack a sugar cube: a fainter violet one. Bang two quartz pebbles: orange glow.\n\n"
                    + "When some crystals crack, the two new surfaces come apart carrying opposite charges (many such crystals are piezoelectric, and fracture is very uneven). "
                    + "The field across the fresh gap is strong enough for a tiny spark through the air.\n\n"
                    + "Nitrogen in that spark glows mostly in ULTRAVIOLET (337 nm) - invisible. Wintergreen oil (methyl salicylate) absorbs that UV and fluoresces blue, so wintergreen is the bright one.\n\n"
                    + "The Ute people of Colorado made quartz-filled rawhide rattles that flashed at night.\n\n"
                    + "<color=#9FB4C4>Model: perceptual brightness ~ log(1/2 m v^2); breaks above 0.8 m/s (tuned); spectra are qualitative.</color>",
                Math =
                    "<b>Impact energy</b>   E = 1/2 m v^2   (mint 2.5 g, sugar cube 4 g, quartz 15 g)\n"
                    + "<b>Drop pestle</b>   v = sqrt(2 g h) = sqrt(2 x 9.81 x 0.16) = 1.8 m/s\n"
                    + "<b>N2 second positive system</b>   337.1, 357.7, 380.5 nm (UV)\n"
                    + "<b>Methyl salicylate</b>   absorbs ~300-340 nm, fluoresces ~450 nm (blue)\n"
                    + "<b>Why a spark?</b>   fresh charged faces 1 um apart: even ~1 V/um = 1 MV/m nears air breakdown (Paschen)\n\n"
                    + "[Sweeting 1990, Chem. Mater. 2, 'Triboluminescence with and without air'; Walton 1977, Adv. Phys. 26; Camara et al. 2008 Nature 455 (X-rays from peeling tape)]",
                TryThis =
                    "1. Pick up a white mint from the tray and smash it on the anvil inside the dark hood (hard!). Blue flash + crunch.\n"
                    + "2. Or put one on the anvil and press DROP PESTLE.\n"
                    + "3. Compare a sugar cube: much fainter, the spectrum bars sit in the UV (you mostly can't see it).\n"
                    + "4. Bang the two quartz pebbles together (or one on the anvil): warm orange.\n"
                    + "5. Desktop: hold a candy and click to crunch it in your hand.\n"
                    + "6. Restock brings fresh candy. Toolbelt CRYSTAL > Wintergreen / Sugar Cube spawns more anywhere.",
                Lattice =
                    "A crystal is held together by charges in a regular pattern. A crack runs between layers and rarely splits the charge evenly: "
                    + "one face keeps slightly more +, the other more - (model: the halves part with an arrow for the field).\n\n"
                    + "Sugar (sucrose) crystals are piezoelectric, which helps: the stress right before fracture already pushes charge to the surfaces.\n\n"
                    + "The gap is microns wide, so even small voltages make enormous fields - the air in the crack breaks down, and the excited N2 molecules emit light as they relax."
            };
        }

        protected override void AddControls(CrystalGuideCard card)
        {
            card.AddControl("Drop pestle", DropPestle);
            card.AddControl("Mint on anvil", () => _stock.Add(TriboCandy.Create(TriboCandy.Kind.Wintergreen, _anvilTop.position + Vector3.up * 0.01f, "Crystal_Candy_Anvil")));
            card.AddControl("Sugar on anvil", () => _stock.Add(TriboCandy.Create(TriboCandy.Kind.Sugar, _anvilTop.position + Vector3.up * 0.012f, "Crystal_Candy_Anvil")));
            card.AddControl("Quartz on anvil", () => _stock.Add(TriboCandy.Create(TriboCandy.Kind.Quartz, _anvilTop.position + Vector3.up * 0.015f, "Crystal_Candy_Anvil")));
            card.AddControl("Restock", Restock);
            _doorChip = card.AddControl("Visor: DOWN", () =>
            {
                _door = !_door;
                _hoodDoor.gameObject.SetActive(_door);
                CrystalGuideCard.SetChipLabel(_doorChip, _door ? "Visor: DOWN" : "Visor: UP");
            });
        }

        protected override string LiveLine()
        {
            return "last flash: " + _lastKind + "   brightness " + (_lastB * 100f).ToString("0") + "%   break speed > " + TriboCandy.BreakSpeed.ToString("0.0")
                + " m/s   pestle drop 1.8 m/s\nwintergreen = N2 UV 337 nm -> blue 450 nm fluorescence   sugar = bare N2 (violet / UV)   quartz = orange";
        }
    }
}
