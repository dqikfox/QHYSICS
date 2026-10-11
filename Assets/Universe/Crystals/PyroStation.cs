using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.Audio;
using C = RealityEngine.Crystals.CrystalConstants;

namespace RealityEngine.Crystals
{
    /// <summary>
    /// Pyroelectric crystal. Its spontaneous polarisation Ps depends on temperature; normally stray ions from the
    /// air sit on the faces and cancel the bound charge. Change T and the bound charge changes faster than the
    /// screening can follow: d(sigma)/dt = p dT/dt - sigma / tau_screen.
    /// Honesty: lumped temperature (Newton heating toward CrystalHeatSource environment); surface charge clamped
    /// at the air corona limit sigma_max = 2 eps0 E_bd ~ 5.3e-5 C/m^2 (in vacuum real crystals go far higher -
    /// that is how pyroelectric X-ray sources and "pyrofusion" work). tau_screen = 30 s (dry lab air, assumed).
    /// Sign convention: heating makes the +c (top) face NEGATIVE (p is negative for LiTaO3 and tourmaline).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PyroCrystal : MonoBehaviour, ICrystalInspectable
    {
        public const double ScreenTau = 30.0;
        public static double SigmaMax => 2.0 * C.Eps0 * C.AirBreakdown;
        public bool Tourmaline;
        public float AmbientC = 22f;
        public float ThermalTau = 60f;   // s, still air only
        public double TempC = 22.0;
        public double Sigma;             // C/m^2 on the +c face (net, unscreened)
        public double LastRateKps { get; private set; }
        Renderer _r;
        Material _cold, _mid, _hot;
        int _shade = -1;
        bool _clamped;

        public string MaterialName => Tourmaline ? "tourmaline (schorl)" : "LiTaO3 (z-cut)";
        public double P => Tourmaline ? C.TourmalineP : C.LiTaO3P;
        public double EpsR => Tourmaline ? C.TourmalineEpsR : C.LiTaO3EpsR;
        /// <summary>LiTaO3 30 x 30 x 5 mm plate; tourmaline 15 mm dia x 40 mm prism (c-axis along the length).</summary>
        public double Area => Tourmaline ? Math.PI * 0.0075 * 0.0075 : 9e-4;
        public double Thick => Tourmaline ? 0.04 : 0.005;
        public double EquivRadius => Math.Sqrt(Area / Math.PI);
        public bool Clamped => _clamped;
        /// <summary>Potential across the crystal V = sigma t / (eps0 epsR).</summary>
        public double InternalVolts => Sigma * Thick / (C.Eps0 * EpsR);
        /// <summary>On-axis field of a uniformly charged disc of radius R at height z: sigma/2eps0 (1 - z / sqrt(z^2+R^2)).</summary>
        public double FieldAt(double z)
        {
            double r = EquivRadius;
            return Math.Abs(Sigma) / (2.0 * C.Eps0) * (1.0 - z / Math.Sqrt(z * z + r * r));
        }

        public void Init(Renderer r)
        {
            _r = r;
            _cold = CrystalFx.Crystal(new Color(0.6f, 0.8f, 1f, 0.55f), new Color(0.05f, 0.1f, 0.25f));
            _mid = Tourmaline ? CrystalFx.Crystal(new Color(0.15f, 0.25f, 0.2f, 0.75f), new Color(0.02f, 0.04f, 0.03f))
                              : CrystalFx.Crystal(new Color(0.9f, 0.92f, 0.95f, 0.45f), new Color(0.04f, 0.05f, 0.06f));
            _hot = CrystalFx.Crystal(new Color(1f, 0.7f, 0.45f, 0.55f), new Color(0.35f, 0.12f, 0.02f));
            _shade = -1;
        }

        public void ResetState(double tempC)
        {
            TempC = tempC;
            Sigma = 0;
            LastRateKps = 0;
        }

        void Update()
        {
            double dt = Math.Min(0.1, Time.deltaTime);
            if (dt <= 0)
                return;
            float env = CrystalHeatSource.EnvironmentAt(transform.position, AmbientC, out float coupling);
            double rate = (env - TempC) * coupling / ThermalTau;
            TempC += rate * dt;
            LastRateKps = rate;
            Sigma += -P * rate * dt;
            Sigma *= Math.Exp(-dt / ScreenTau);
            double sm = SigmaMax;
            _clamped = Math.Abs(Sigma) >= sm;
            if (_clamped)
                Sigma = Math.Sign(Sigma) * sm;
            int shade = TempC > AmbientC + 25 ? 2 : (TempC < AmbientC - 15 ? 0 : 1);
            if (_r != null && shade != _shade)
            {
                _shade = shade;
                _r.sharedMaterial = shade == 2 ? _hot : (shade == 0 ? _cold : _mid);
            }
        }

        public string InspectStats(bool held) =>
            MaterialName + " | " + TempC.ToString("0.0") + " C | top face " + (Sigma < 0 ? "-" : "+") + " | sigma " + (Math.Abs(Sigma) * 1e6).ToString("0.0") + " uC/m2";
        public string InspectDetail() =>
            "p = " + (P * 1e6).ToString("0.#") + " uC/m2K | dT/dt " + LastRateKps.ToString("0.00") + " K/s | E(0) " + (FieldAt(0) / 1e6).ToString("0.00") + " MV/m" + (_clamped ? " (corona limit)" : "");
        public string InspectHint(bool held) => "Heat or cool it: charge appears only while T changes";
    }

    /// <summary>Station 2: heat / cool a pyroelectric plate over a tray of tissue-paper bits, with an electroscope.</summary>
    public sealed class PyroStation : CrystalStation
    {
        public override string Id => "pyro";
        public override string Title => "Pyroelectricity";
        public override string ChallengeId => "crystal_pyro";
        protected override CrystalUnitCell.Mode CellMode => CrystalUnitCell.Mode.PyroLiTaO3;
        protected override string CellTitle => "LiTaO3: Li(+) Ta(+) in O(-) cage";

        // Crystal underside to the paper (m): the slim tourmaline prism must come closer (its field falls off faster).
        float BitGap => _pyro != null && _pyro.Tourmaline ? 0.008f : 0.025f;
        const float PaperAreal = 0.018f;      // kg/m^2 (tissue paper 17-20 g/m^2)
        const float ProbeDist = 0.05f, ProbeGap = 0.01f;
        PyroCrystal _pyro;
        Transform _crystal, _holder;
        CrystalHeatSource _lamp, _cold;
        Light _lampLight;
        Renderer _bulb, _coldR;
        Material _bulbOn, _bulbOff, _coldOn, _coldOff;
        Transform _leafA, _leafB;
        TextMeshPro _thermo;
        readonly List<Bit> _bits = new List<Bit>();
        float _tickAcc;
        UnityEngine.UI.Button _lampChip, _coldChip, _matChip;
        int _lifted;
        double _ePa;

        sealed class Bit
        {
            public Transform T;
            public Vector3 Rest;
            public float Need;     // threshold multiplier
            public float State;    // 0 rest .. 1 stuck
            public float StuckFor;
            public float CoolDown;
        }

        float CrystalBottomY => _crystal.localPosition.y - (float)(_pyro.Tourmaline ? 0.04 : 0.02) * 0.5f;

        protected override void BuildStation()
        {
            Material steel = CrystalFx.Solid(new Color(0.55f, 0.58f, 0.62f), 0.85f, 0.55f);
            Material dark = CrystalFx.Solid(new Color(0.12f, 0.13f, 0.15f), 0.6f, 0.5f);
            Material glass = CrystalFx.Crystal(new Color(0.85f, 0.95f, 1f, 0.18f));

            // Holder: an insulating acrylic arm holding the crystal over the paper tray (centre).
            _holder = new GameObject("Holder").transform;
            _holder.SetParent(transform, false);
            _holder.localPosition = OnTop(0f, 0f, 0.05f);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Post", _holder, new Vector3(-0.12f, 0.05f, 0f), new Vector3(0.02f, 0.05f, 0.02f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "Arm", _holder, new Vector3(-0.06f, 0.1f, 0f), new Vector3(0.14f, 0.012f, 0.025f), glass, false);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Clamp", _holder, new Vector3(0f, 0.08f, 0f), new Vector3(0.012f, 0.022f, 0.012f), glass, false);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Tray", _holder, new Vector3(0f, 0.004f, 0f), new Vector3(0.12f, 0.004f, 0.12f), dark);
            var cr = CrystalFx.Prim(PrimitiveType.Cube, "PyroPlate", _holder, Vector3.zero, new Vector3(0.06f, 0.02f, 0.06f), glass);
            _crystal = cr.transform;
            _pyro = cr.AddComponent<PyroCrystal>();
            _pyro.Init(cr.GetComponent<Renderer>());
            cr.AddComponent<XRSimpleInteractable>();
            PlaceCrystal();

            var bitMat = CrystalFx.Solid(new Color(0.96f, 0.95f, 0.9f), 0f, 0.2f);
            var rnd = new System.Random(7);
            for (int i = 0; i < 18; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = (float)Math.Sqrt(rnd.NextDouble()) * 0.045f;
                var p = new Vector3(Mathf.Cos(a) * r, 0.0095f, Mathf.Sin(a) * r);
                var g = CrystalFx.Prim(PrimitiveType.Cube, "PaperBit", _holder, p, new Vector3(0.006f, 0.0008f, 0.005f), bitMat, false);
                g.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f);
                _bits.Add(new Bit { T = g.transform, Rest = p, Need = 0.6f + (float)rnd.NextDouble() * 0.9f });
            }

            // Heat lamp (left), cold block (right).
            var lamp = new GameObject("HeatLamp").transform;
            lamp.SetParent(transform, false);
            lamp.localPosition = OnTop(-0.32f, 0f, 0.05f);
            CrystalFx.Prim(PrimitiveType.Cylinder, "LampPost", lamp, new Vector3(0f, 0.16f, 0f), new Vector3(0.02f, 0.16f, 0.02f), dark);
            var head = CrystalFx.Prim(PrimitiveType.Cylinder, "LampHead", lamp, new Vector3(0.11f, 0.3f, 0f), new Vector3(0.09f, 0.03f, 0.09f), steel);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            _bulbOff = CrystalFx.Solid(new Color(0.35f, 0.2f, 0.15f), 0f, 0.6f);
            _bulbOn = CrystalFx.Emissive(new Color(1f, 0.35f, 0.1f), 2.2f);
            var bulb = CrystalFx.Prim(PrimitiveType.Sphere, "Bulb", lamp, new Vector3(0.125f, 0.27f, 0f), Vector3.one * 0.06f, _bulbOff, false);
            _bulb = bulb.GetComponent<Renderer>();
            _lamp = bulb.AddComponent<CrystalHeatSource>();
            _lamp.TempC = 140f; _lamp.Radius = 0.42f; _lamp.On = false;
            _lampLight = new GameObject("LampLight").AddComponent<Light>();
            _lampLight.transform.SetParent(lamp, false);
            _lampLight.transform.localPosition = new Vector3(0.14f, 0.24f, 0f);
            _lampLight.type = LightType.Point; _lampLight.range = 0.8f; _lampLight.intensity = 1.2f;
            _lampLight.color = new Color(1f, 0.5f, 0.25f); _lampLight.shadows = LightShadows.None; _lampLight.enabled = false;
            var lt = CrystalFx.Prim(PrimitiveType.Cube, "LampSwitch", lamp, new Vector3(0f, 0.02f, -0.06f), new Vector3(0.05f, 0.03f, 0.04f), CrystalFx.Solid(new Color(0.9f, 0.5f, 0.1f))).AddComponent<CrystalToggle>();
            lt.Label = "Heat lamp"; lt.Toggled = ToggleLamp;

            var coldRoot = new GameObject("ColdBlock").transform;
            coldRoot.SetParent(transform, false);
            coldRoot.localPosition = OnTop(0.2f, 0f, 0.05f);
            _coldOff = CrystalFx.Solid(new Color(0.4f, 0.5f, 0.6f), 0.6f, 0.6f);
            _coldOn = CrystalFx.Solid(new Color(0.75f, 0.9f, 1f), 0.2f, 0.85f, new Color(0.1f, 0.2f, 0.35f));
            var cb = CrystalFx.Prim(PrimitiveType.Cube, "ColdPlate", coldRoot, new Vector3(-0.07f, 0.13f, 0f), new Vector3(0.04f, 0.16f, 0.1f), _coldOff);
            _coldR = cb.GetComponent<Renderer>();
            _cold = cb.AddComponent<CrystalHeatSource>();
            _cold.TempC = -40f; _cold.Radius = 0.3f; _cold.On = false;
            var ct = CrystalFx.Prim(PrimitiveType.Cube, "ColdSwitch", coldRoot, new Vector3(0f, 0.02f, -0.06f), new Vector3(0.05f, 0.03f, 0.04f), CrystalFx.Solid(new Color(0.2f, 0.55f, 0.95f))).AddComponent<CrystalToggle>();
            ct.Label = "Cold plate"; ct.Toggled = ToggleCold;

            // Gold-leaf electroscope (right end).
            var es = new GameObject("Electroscope").transform;
            es.SetParent(transform, false);
            es.localPosition = OnTop(0.55f, 0f, 0.08f);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Jar", es, new Vector3(0f, 0.09f, 0f), new Vector3(0.12f, 0.09f, 0.12f), glass, true);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Rod", es, new Vector3(0f, 0.17f, 0f), new Vector3(0.006f, 0.08f, 0.006f), steel, false);
            CrystalFx.Prim(PrimitiveType.Sphere, "Cap", es, new Vector3(0f, 0.255f, 0f), Vector3.one * 0.035f, steel, false);
            Material gold = CrystalFx.Solid(new Color(1f, 0.8f, 0.3f), 1f, 0.8f, new Color(0.15f, 0.1f, 0.02f));
            _leafA = Leaf(es, gold, "LeafA");
            _leafB = Leaf(es, gold, "LeafB");
            CrystalFx.Text(es, "EsLabel", "electroscope\n(probe 5 cm from plate)", 0.13f, new Color(0.6f, 0.9f, 1f, 0.9f), new Vector3(0f, 0.31f, 0f), new Vector2(0.3f, 0.07f));

            _thermo = CrystalFx.Text(transform, "Thermo", "", 0.18f, new Color(0.55f, 1f, 0.85f), OnTop(-0.62f, 0.15f, 0.05f), new Vector2(0.4f, 0.24f), TextAlignmentOptions.TopLeft);
            CrystalFx.Prim(PrimitiveType.Cube, "ThermoPanel", transform, OnTop(-0.62f, 0.14f, 0.075f), new Vector3(0.4f, 0.26f, 0.04f), dark, true);
        }

        Transform Leaf(Transform parent, Material m, string name)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0f, 0.13f, 0f);
            CrystalFx.Prim(PrimitiveType.Cube, "Foil", pivot, new Vector3(0f, -0.035f, 0f), new Vector3(0.012f, 0.07f, 0.0005f), m, false);
            return pivot;
        }

        void PlaceCrystal()
        {
            float h = _pyro.Tourmaline ? 0.04f : 0.02f;
            _crystal.localScale = _pyro.Tourmaline ? new Vector3(0.03f, 0.04f, 0.03f) : new Vector3(0.06f, 0.02f, 0.06f);
            _crystal.localPosition = new Vector3(0f, 0.01f + BitGap + h * 0.5f, 0f);
        }

        void ToggleLamp()
        {
            _lamp.On = !_lamp.On;
            if (_lamp.On && _cold.On) ToggleCold();
            _lampLight.enabled = _lamp.On;
            _bulb.sharedMaterial = _lamp.On ? _bulbOn : _bulbOff;
            QhysicsSfx.PlayAt(SfxId.UiClick, _bulb.transform.position, 0.5f);
            CrystalGuideCard.SetChipLabel(_lampChip, _lamp.On ? "Lamp: ON" : "Lamp: OFF");
        }

        void ToggleCold()
        {
            _cold.On = !_cold.On;
            if (_cold.On && _lamp.On) ToggleLamp();
            _coldR.sharedMaterial = _cold.On ? _coldOn : _coldOff;
            QhysicsSfx.PlayAt(SfxId.UiClick, _coldR.transform.position, 0.5f);
            CrystalGuideCard.SetChipLabel(_coldChip, _cold.On ? "Cold: ON" : "Cold: OFF");
        }

        void Update()
        {
            if (_pyro == null)
                return;
            float dt = Time.deltaTime;
            float bottom = CrystalBottomY;
            // Field-pressure lift test: eps0 E^2 / 2 > m g / A (paper treated as a conductor - it is slightly damp).
            double needE = Math.Sqrt(2.0 * PaperAreal * 9.81 / C.Eps0);
            int lifted = 0;
            for (int i = 0; i < _bits.Count; i++)
            {
                Bit b = _bits[i];
                double e = _pyro.FieldAt(BitGap + 0.0005);
                bool pull = e > needE * b.Need && b.CoolDown <= 0f;
                if (b.State >= 1f)
                {
                    b.StuckFor += dt;
                    // Touching the face the bit picks up same-sign charge and is flung off (the classic "dance").
                    if (b.StuckFor > 0.6f + b.Need || !pull)
                    {
                        b.State = 0.99f;
                        b.CoolDown = 0.4f + b.Need * 0.5f;
                        b.StuckFor = 0f;
                        if (pull) QhysicsSfx.PlayAt(SfxId.PyroTick, b.T.position, 0.18f, 0.9f + b.Need * 0.3f);
                    }
                }
                if (b.CoolDown > 0f) b.CoolDown -= dt;
                float target = pull ? 1f : 0f;
                float speed = pull ? 6f : 3.5f;
                b.State = Mathf.MoveTowards(b.State, target, dt * speed);
                if (b.State >= 1f && pull && b.StuckFor == 0f)
                {
                    b.StuckFor = 0.0001f;
                    QhysicsSfx.PlayAt(SfxId.PyroTick, b.T.position, 0.22f, 1f + b.Need * 0.3f);
                }
                float top = bottom - 0.0006f;
                Vector3 stuckPos = new Vector3(b.Rest.x * 0.55f, top, b.Rest.z * 0.55f);
                float s = b.State * b.State * (3f - 2f * b.State);
                b.T.localPosition = Vector3.Lerp(b.Rest, stuckPos, s) + new Vector3(0f, Mathf.Sin(s * Mathf.PI) * 0.006f, 0f);
                if (b.State > 0.5f) lifted++;
            }
            _lifted = lifted;
            if (lifted >= 3)
            {
                if (_pyro.Sigma < 0) CrystalGoals.Latch("pyro_lift");
                else CrystalGoals.Latch("pyro_reverse");
            }

            // Electroscope: cap charges by induction to roughly V ~ E(probe) x d_gap; leaves spread with |V|.
            _ePa = _pyro.FieldAt(ProbeDist);
            double vProbe = _ePa * ProbeGap;
            float ang = 80f * (1f - Mathf.Exp(-(float)vProbe / 3000f));
            _leafA.localRotation = Quaternion.Euler(0f, 0f, ang * 0.5f);
            _leafB.localRotation = Quaternion.Euler(0f, 0f, -ang * 0.5f);

            if (Cell != null)
            {
                Cell.Drive = 1f;
                Cell.Aux = Mathf.Clamp01(((float)_pyro.TempC + 30f) / 260f);
                Cell.SetCaption(Math.Abs(_pyro.LastRateKps) < 0.05 ? "steady T: surface ions\nscreen the dipoles" :
                    (_pyro.LastRateKps > 0 ? "heating: Ps shrinks,\nscreen charge left over" : "cooling: Ps grows,\nbound charge uncovered"));
            }

            _tickAcc += dt;
            if (_tickAcc > 0.15f && _thermo != null)
            {
                _tickAcc = 0f;
                string sign = Math.Abs(_pyro.Sigma) < 1e-7 ? "0" : (_pyro.Sigma < 0 ? "<color=#6FA8FF>-</color>" : "<color=#FF6A5A>+</color>");
                _thermo.text = "<b>" + _pyro.MaterialName + "</b>\nT " + _pyro.TempC.ToString("0.0") + " C   dT/dt " + _pyro.LastRateKps.ToString("+0.00;-0.00") + " K/s\n"
                    + "top face " + sign + "  " + (Math.Abs(_pyro.Sigma) * 1e6).ToString("0.0") + " uC/m2" + (_pyro.Clamped ? "  <color=#FFD24A>corona!</color>" : "")
                    + "\nE at bits " + (_pyro.FieldAt(BitGap) / 1e3).ToString("0") + " kV/m (need ~" + (needE / 1e3).ToString("0") + ")\nbits up " + _lifted + " / " + _bits.Count;
            }
        }

        protected override CrystalGuideCard.Content GetContent()
        {
            return new CrystalGuideCard.Content
            {
                Title = "2  PYROELECTRICITY  - change the temperature, get a charge",
                ChallengeId = ChallengeId,
                What =
                    "Some crystals are permanently polarised: every cell is a tiny dipole all pointing the same way (along the 'c axis'). "
                    + "You normally cannot tell, because stray ions from the air settle on the faces and cancel it.\n\n"
                    + "Warm the crystal and the ions jiggle, the polarisation shrinks, and the cancelling ions are suddenly too many: one face goes negative, the other positive. "
                    + "Cool it and the signs flip. Hold the temperature steady and the air slowly neutralises it again.\n\n"
                    + "Tourmaline was famous for this in the 1700s: Dutch traders called it 'ash puller' because a stone warmed by the fire grabbed the ashes. "
                    + "Today LiTaO3 chips sit in every PIR motion sensor.\n\n"
                    + "<color=#9FB4C4>Model: lumped temperature; charge clamped at the ~3 MV/m air corona limit; paper bits treated as conductors.</color>",
                Math =
                    "<b>Charge rate</b>   d(sigma)/dt = p dT/dt - sigma / tau     (tau = 30 s, assumed)\n"
                    + "<b>Clamp</b>   |sigma| <= 2 eps0 E_bd = 5.3e-5 C/m2\n"
                    + "<b>Field on axis</b>   E(z) = sigma/2eps0 (1 - z / sqrt(z^2 + R^2))\n"
                    + "<b>Paper lifts when</b>   eps0 E^2 / 2 > m g / A  ->  E > ~190 kV/m\n"
                    + "<b>Heating</b>   dT/dt = (T_env - T) k / 60 s   (k = coupling to lamp / plate)\n\n"
                    + "LiTaO3: p = -2.3e-4 C/m2K, epsR 43, Curie 603 C  [Glass & Lines 1976; Lang, Physics Today 2005]\n"
                    + "tourmaline: p ~ -4e-6 C/m2K, epsR ~7  [Lang 2005]\n"
                    + "tissue paper 17-20 g/m2; eps0 = 8.854e-12 F/m; air E_bd ~3 MV/m",
                TryThis =
                    "1. Look at the thermometer panel: at a steady 22 C there is no charge.\n"
                    + "2. Switch the heat lamp ON (orange switch or Lamp chip). Within seconds the paper bits leap up and dance on the plate.\n"
                    + "3. Watch the electroscope leaves spread.\n"
                    + "4. Leave the lamp on: once T stops rising, the charge drains (tau 30 s) and the bits fall.\n"
                    + "5. Lamp OFF, Cold ON (a -40 C Peltier plate): cooling makes the OPPOSITE charge (+), and the bits dance again - paper does not care about the sign.\n"
                    + "6. Switch to tourmaline: ~60x weaker p and a slim prism, so it is held 8 mm over the paper instead of 25 mm. Still enough!",
                Lattice =
                    "LiTaO3: an oxygen cage (blue) holds Ta(+) and Li(+) ions that sit slightly OFF-centre along the c axis (the model shows this, exaggerated).\n\n"
                    + "That built-in offset = a permanent dipole in every cell, Ps.\n"
                    + "Heat it: thermal jiggling makes the average offset smaller, so Ps drops. The surface ions that were cancelling the OLD Ps are now too many -> net charge.\n"
                    + "Above the Curie point (603 C) the ions would sit dead centre and Ps would vanish completely.\n\n"
                    + "Every pyroelectric crystal is also piezoelectric (not the other way round: quartz is piezo but not pyro, it has no single polar axis)."
            };
        }

        protected override void AddControls(CrystalGuideCard card)
        {
            _lampChip = card.AddControl("Lamp: OFF", ToggleLamp);
            _coldChip = card.AddControl("Cold: OFF", ToggleCold);
            _matChip = card.AddControl("Crystal: LiTaO3", () =>
            {
                _pyro.Tourmaline = !_pyro.Tourmaline;
                _pyro.Init(_crystal.GetComponent<Renderer>());
                _pyro.ResetState(_pyro.AmbientC);
                PlaceCrystal();
                CrystalGuideCard.SetChipLabel(_matChip, _pyro.Tourmaline ? "Crystal: Tourmal." : "Crystal: LiTaO3");
            });
            card.AddControl("Heat +15 C", () => { _pyro.Sigma -= _pyro.P * 15.0; _pyro.TempC += 15.0; QhysicsSfx.PlayAt(SfxId.FireCast, _crystal.position, 0.3f, 1.4f); });
            card.AddControl("Room temp", () => _pyro.ResetState(_pyro.AmbientC));
            card.AddControl("Neutralise", () => { _pyro.Sigma = 0; QhysicsSfx.PlayAt(SfxId.UiClick, _crystal.position, 0.4f); });
        }

        protected override string LiveLine()
        {
            var p = _pyro;
            return p.MaterialName + ":  T " + p.TempC.ToString("0.0") + " C   dT/dt " + p.LastRateKps.ToString("+0.00;-0.00") + " K/s   sigma "
                + (p.Sigma * 1e6).ToString("+0.0;-0.0") + " uC/m2   E(bits) " + (p.FieldAt(BitGap) / 1e3).ToString("0") + " kV/m   V(internal) "
                + CrystalFx.FormatVolts(p.InternalVolts) + "\nlamp " + (_lamp.On ? "ON" : "off") + "   cold plate " + (_cold.On ? "ON" : "off")
                + "   electroscope ~" + CrystalFx.FormatVolts(_ePa * ProbeGap) + "   bits up " + _lifted;
        }
    }
}
