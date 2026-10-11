using System;
using TMPro;
using UnityEngine;
using RealityEngine.Audio;
using C = RealityEngine.Crystals.CrystalConstants;

namespace RealityEngine.Crystals
{
    /// <summary>
    /// Station 8: the twisted-nematic (TN) liquid-crystal cell - how every digital watch and calculator display works.
    /// Backlight -> rear polariser (0 deg) -> 5CB cell with a 90 deg twist -> front analyser (rotatable). A voltage dial
    /// tilts the molecules up (Freedericksz transition) and kills the twist that guides the light; a heat lamp melts the
    /// nematic order above T_NI = 35.3 C. A two-digit seconds display (normally white, driven segments go dark) ticks at
    /// 32 768 Hz / 2^15 = 1 Hz - the same watch crystal as bench 3.
    /// Physics: threshold V_th = pi sqrt((K11 + (K33 - 2 K22)/4) / (eps0 d_eps)) (~0.85 V for 5CB, independent of the gap);
    /// zero-field guiding leakage from Gooch-Tarry, L = sin^2(pi/2 sqrt(1+u^2)) / (1+u^2), u = 2 d dn / lambda;
    /// order parameter S(T) = S0 (1 - T/T*)^0.2 (Haller); dn ~ S, K ~ S^2, d_eps ~ S so V_th ~ sqrt(S); response times
    /// tau_off = g1 d^2 / (K pi^2), tau_on = g1 d^2 / (eps0 d_eps (V^2 - V_th^2)).
    /// Honesty: the electro-optic curve above threshold is a fitted Lorentzian in (V - V_th) (V50 ~ 1.35 V_th), not a
    /// director-profile solve; rotated / unrotated light are added incoherently; 'ion damage' under DC is a cartoon of
    /// real electrolysis / image sticking.
    /// </summary>
    public sealed class LiquidCrystalStation : CrystalStation
    {
        public override string Id => "lcd";
        public override string Title => "Liquid Crystal Display";
        public override string ChallengeId => "crystal_lcd";
        protected override CrystalUnitCell.Mode CellMode => CrystalUnitCell.Mode.Nematic;
        protected override string CellTitle => "5CB nematic: rods twist 90 deg across the cell";

        const double Lambda = 550e-9;
        const float VMax = 6f;
        const double AmbientC = 25.0;

        double _v;               // set Vrms
        double _vEff;            // what the molecules have responded to (relaxes with tau_on / tau_off)
        double _gap = 5e-6;      // cell gap, m
        double _tempC = AmbientC;
        double _analyzerDeg = 90; // 90 = crossed (normally white)
        bool _dc;
        double _ionDamage;       // 0..0.7 contrast loss under DC
        bool _clock = true;

        CrystalDial _vDial, _aDial;
        CrystalHeatSource _lamp;
        Renderer _lampBulb;
        Material _bulbOn, _bulbOff;
        Material _cellMat, _segOnMat, _segOffMat;
        Transform _analyzerRing;
        readonly Renderer[] _segs = new Renderer[14];
        TextMeshPro _readout, _dispLabel;
        float _tick;
        int _shown = -1;
        UnityEngine.UI.Button _lampChip, _gapChip, _driveChip, _clockChip;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        // Segment patterns a..g for digits 0-9 (bit 0 = a ... bit 6 = g).
        static readonly int[] Digit = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

        // ── Physics ──

        /// <summary>Haller order parameter, normalised so S(25 C) ~ 0.60; 0 above T_NI (first-order jump).</summary>
        public static double Order(double tempC)
        {
            if (tempC >= C.Lc5cbTniC)
                return 0.0;
            double tk = tempC + 273.15, tStar = C.Lc5cbTniC + 273.15 + 1.0;
            return Math.Min(0.85, 1.16 * Math.Pow(Math.Max(1e-6, 1.0 - tk / tStar), C.HallerBeta));
        }

        static double S25 => Order(25.0);
        public double S => Order(_tempC);
        public bool Isotropic => _tempC >= C.Lc5cbTniC;
        public double Dn => C.Lc5cbDn * S / S25;

        /// <summary>TN Freedericksz threshold at 25 C (V rms).</summary>
        public static double VthAt25 => Math.PI * Math.Sqrt((C.Lc5cbK11 + (C.Lc5cbK33 - 2.0 * C.Lc5cbK22) / 4.0) / (C.Eps0 * C.Lc5cbDeps));
        public double Vth => Isotropic ? double.PositiveInfinity : VthAt25 * Math.Sqrt(Math.Max(1e-3, S / S25));
        double Keff => (C.Lc5cbK11 + (C.Lc5cbK33 - 2.0 * C.Lc5cbK22) / 4.0) * Math.Pow(S / S25, 2);
        double Deps => C.Lc5cbDeps * S / S25;
        public double TauOff => Isotropic ? 0.0 : C.Lc5cbGamma1 * _gap * _gap / (Math.Max(1e-15, Keff) * Math.PI * Math.PI);
        public double TauOn(double v) => Isotropic || v <= Vth ? double.PositiveInfinity : C.Lc5cbGamma1 * _gap * _gap / (C.Eps0 * Deps * (v * v - Vth * Vth));
        public double U => 2.0 * _gap * Dn / Lambda;
        /// <summary>Gooch-Tarry: fraction of light NOT rotated by the twist at zero field.</summary>
        public double Leak { get { double u2 = 1.0 + U * U; double s = Math.Sin(Math.PI * 0.5 * Math.Sqrt(u2)); return s * s / u2; } }

        /// <summary>Fraction of the twist-guided light at an effective drive voltage (1 below threshold).</summary>
        double Guided(double vEff)
        {
            if (Isotropic)
                return 0.0;
            double vth = Vth;
            double g = vEff <= vth ? 1.0 : 1.0 / (1.0 + Math.Pow((vEff - vth) / (0.35 * vth), 2.0));
            return g * (1.0 - Leak) * (1.0 - _ionDamage);
        }

        /// <summary>Transmission relative to parallel polarisers with no cell (0..1).</summary>
        public double Transmission(double vEff)
        {
            double r = Guided(vEff);
            double phi = _analyzerDeg * Math.PI / 180.0;
            double sin2 = Math.Sin(phi) * Math.Sin(phi), cos2 = 1.0 - sin2;
            return r * sin2 + (1.0 - r) * cos2;
        }

        public double T0 => Transmission(0.0);
        public double TNow => Transmission(_vEff);

        // ── Build ──

        protected override void BuildStation()
        {
            Material dark = CrystalFx.Solid(new Color(0.1f, 0.11f, 0.13f), 0.5f, 0.5f);
            Material frame = CrystalFx.Solid(new Color(0.2f, 0.22f, 0.25f), 0.7f, 0.6f);
            Material polar = CrystalFx.Crystal(new Color(0.35f, 0.3f, 0.5f, 0.45f));
            Material glass = CrystalFx.Crystal(new Color(0.85f, 0.95f, 1f, 0.18f));
            Material lineMat = CrystalFx.Line(new Color(0.15f, 0.12f, 0.25f, 1f));
            _cellMat = new Material(CrystalFx.Emissive(new Color(0.95f, 0.98f, 1f), 1.2f)) { name = "LcdCell" };
            _segOnMat = new Material(_cellMat) { name = "LcdSegOn" };
            _segOffMat = new Material(_cellMat) { name = "LcdSegOff" };

            // Optical stack, viewed from the player side (-Z): backlight, rear polariser, LC cell (the window), analyser ring.
            var stack = new GameObject("LcStack").transform;
            stack.SetParent(transform, false);
            stack.localPosition = OnTop(-0.38f, 0.24f, 0.05f);
            CrystalFx.Prim(PrimitiveType.Cube, "Stand", stack, new Vector3(0f, -0.13f, 0.06f), new Vector3(0.06f, 0.22f, 0.06f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "Backlight", stack, new Vector3(0f, 0f, 0.09f), new Vector3(0.40f, 0.30f, 0.02f), CrystalFx.Emissive(new Color(1f, 0.98f, 0.92f), 1.6f));
            CrystalFx.Prim(PrimitiveType.Cube, "RearPolariser", stack, new Vector3(0f, 0f, 0.07f), new Vector3(0.38f, 0.28f, 0.004f), polar, false);
            for (int i = 0; i < 7; i++)
                CrystalFx.Prim(PrimitiveType.Cube, "RearAxis" + i, stack, new Vector3(-0.15f + i * 0.05f, 0f, 0.067f), new Vector3(0.002f, 0.26f, 0.001f), lineMat, false);
            var cell = CrystalFx.Prim(PrimitiveType.Cube, "LcCell", stack, new Vector3(0f, 0f, 0.035f), new Vector3(0.34f, 0.24f, 0.004f), _cellMat, false);
            cell.GetComponent<Renderer>().sharedMaterial = _cellMat;
            CrystalFx.Prim(PrimitiveType.Cube, "CellGlassA", stack, new Vector3(0f, 0f, 0.041f), new Vector3(0.36f, 0.26f, 0.003f), glass, false);
            CrystalFx.Prim(PrimitiveType.Cube, "CellGlassB", stack, new Vector3(0f, 0f, 0.029f), new Vector3(0.36f, 0.26f, 0.003f), glass, false);
            CrystalFx.Prim(PrimitiveType.Cube, "Frame", stack, new Vector3(0f, 0.145f, 0.05f), new Vector3(0.42f, 0.02f, 0.08f), frame);
            CrystalFx.Prim(PrimitiveType.Cube, "FrameB", stack, new Vector3(0f, -0.145f, 0.05f), new Vector3(0.42f, 0.02f, 0.08f), frame);
            CrystalFx.Text(stack, "StackLabel", "backlight | polariser 0 deg | 5CB cell (ITO glass) | analyser", 0.12f, new Color(0.6f, 0.9f, 1f),
                new Vector3(0f, 0.18f, -0.02f), new Vector2(0.5f, 0.04f));

            // Analyser: a polariser disc in a ring you twist (grab + roll wrist, or E on desktop).
            _analyzerRing = new GameObject("Analyser").transform;
            _analyzerRing.SetParent(stack, false);
            _analyzerRing.localPosition = new Vector3(0f, 0f, -0.01f);
            var disc = CrystalFx.Prim(PrimitiveType.Cylinder, "Disc", _analyzerRing, Vector3.zero, new Vector3(0.3f, 0.002f, 0.3f), polar, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 5; i++)
                CrystalFx.Prim(PrimitiveType.Cube, "Axis" + i, _analyzerRing, new Vector3(0f, -0.1f + i * 0.05f, -0.002f), new Vector3(0.24f, 0.002f, 0.001f), lineMat, false);
            var ring = CrystalFx.Prim(PrimitiveType.Cylinder, "Ring", _analyzerRing, new Vector3(0f, 0f, 0.001f), new Vector3(0.32f, 0.006f, 0.32f), CrystalFx.Emissive(new Color(0.55f, 0.35f, 1f), 0.6f));
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CrystalFx.FlatCollider(ring);
            _aDial = ring.AddComponent<CrystalDial>();
            _aDial.Label = "Analyser";
            _aDial.Wrap = true;
            _aDial.TurnsForFull = 0.5f;      // a full 0-1 is 180 deg of analyser
            _aDial.DesktopStep = 1f / 12f;   // 15 deg per E
            _aDial.Value01 = 0.5f;           // 90 deg = crossed
            _aDial.Changed = v => _analyzerDeg = v * 180.0;

            // Voltage knob (Vrms, 1 kHz square wave from a function generator box).
            var gen = new GameObject("Generator").transform;
            gen.SetParent(transform, false);
            gen.localPosition = OnTop(-0.05f, 0f, -0.18f);
            CrystalFx.Prim(PrimitiveType.Cube, "Box", gen, new Vector3(0f, 0.04f, 0f), new Vector3(0.16f, 0.08f, 0.1f), dark);
            var knob = CrystalFx.Prim(PrimitiveType.Cylinder, "VKnob", gen, new Vector3(0f, 0.04f, -0.055f), new Vector3(0.045f, 0.012f, 0.045f), CrystalFx.Emissive(new Color(0f, 0.85f, 1f), 0.7f));
            knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _vDial = knob.AddComponent<CrystalDial>();
            _vDial.Label = "Voltage";
            _vDial.TurnsForFull = 1.5f;
            _vDial.DesktopStep = 0.25f / VMax;
            _vDial.Changed = v => _v = v * VMax;
            CrystalFx.Text(gen, "GenLabel", "1 kHz  Vrms", 0.1f, new Color(0.6f, 0.9f, 1f), new Vector3(0f, 0.1f, -0.051f), new Vector2(0.16f, 0.03f));
            // Wires from the generator to the cell's ITO electrodes.
            Material wire = CrystalFx.Line(new Color(0.9f, 0.3f, 0.2f, 1f));
            CrystalFx.Prim(PrimitiveType.Cube, "WireA", transform, OnTop(-0.21f, 0.01f, -0.12f), new Vector3(0.26f, 0.004f, 0.004f), wire, false);
            CrystalFx.Prim(PrimitiveType.Cube, "WireB", transform, OnTop(-0.34f, 0.06f, -0.12f), new Vector3(0.004f, 0.1f, 0.004f), wire, false);

            // Heat lamp over the cell (a CrystalHeatSource, so burners / heat sources nearby also warm the cell).
            var lamp = new GameObject("HeatLamp").transform;
            lamp.SetParent(transform, false);
            lamp.localPosition = OnTop(-0.38f, 0.46f, 0.02f);
            CrystalFx.Prim(PrimitiveType.Cube, "LampArm", transform, OnTop(-0.62f, 0.24f, 0.02f), new Vector3(0.025f, 0.48f, 0.025f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "LampBoom", transform, OnTop(-0.5f, 0.48f, 0.02f), new Vector3(0.26f, 0.02f, 0.02f), dark);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Shade", lamp, new Vector3(0f, 0.02f, 0f), new Vector3(0.1f, 0.03f, 0.1f), frame, false);
            _bulbOff = CrystalFx.Solid(new Color(0.3f, 0.25f, 0.2f), 0f, 0.6f);
            _bulbOn = CrystalFx.Emissive(new Color(1f, 0.4f, 0.1f), 2f);
            _lampBulb = CrystalFx.Bead("Bulb", lamp, new Vector3(0f, -0.01f, 0f), 0.05f, _bulbOff).GetComponent<Renderer>();
            _lamp = lamp.gameObject.AddComponent<CrystalHeatSource>();
            _lamp.TempC = 55f;
            _lamp.Radius = 0.6f;
            _lamp.On = false;
            var lampToggle = CrystalFx.Prim(PrimitiveType.Cube, "LampSwitch", transform, OnTop(-0.62f, 0.02f, -0.15f), new Vector3(0.05f, 0.03f, 0.05f), CrystalFx.Emissive(new Color(1f, 0.45f, 0.1f), 0.6f));
            lampToggle.AddComponent<CrystalToggle>().Toggled = ToggleLamp;
            lampToggle.GetComponent<CrystalToggle>().Label = "Heat lamp";

            // Seven-segment watch display (two digits) on the right, same LC physics.
            var disp = new GameObject("Display").transform;
            disp.SetParent(transform, false);
            disp.localPosition = OnTop(0.38f, 0.2f, 0.08f);
            CrystalFx.Prim(PrimitiveType.Cube, "Bezel", disp, new Vector3(0f, 0f, 0.02f), new Vector3(0.36f, 0.24f, 0.03f), frame);
            var bg = CrystalFx.Prim(PrimitiveType.Cube, "Glass", disp, new Vector3(0f, 0f, 0.004f), new Vector3(0.32f, 0.2f, 0.002f), _segOffMat, false);
            bg.GetComponent<Renderer>().sharedMaterial = _segOffMat;
            for (int d = 0; d < 2; d++)
            {
                float cx = d == 0 ? -0.07f : 0.07f;
                for (int s = 0; s < 7; s++)
                {
                    Vector3 p, sc;
                    const float w = 0.07f, h = 0.06f, t = 0.012f;
                    switch (s)
                    {
                        case 0: p = new Vector3(0f, h, 0f); sc = new Vector3(w, t, 0.001f); break;          // a
                        case 1: p = new Vector3(w * 0.5f, h * 0.5f, 0f); sc = new Vector3(t, h, 0.001f); break; // b
                        case 2: p = new Vector3(w * 0.5f, -h * 0.5f, 0f); sc = new Vector3(t, h, 0.001f); break; // c
                        case 3: p = new Vector3(0f, -h, 0f); sc = new Vector3(w, t, 0.001f); break;          // d
                        case 4: p = new Vector3(-w * 0.5f, -h * 0.5f, 0f); sc = new Vector3(t, h, 0.001f); break; // e
                        case 5: p = new Vector3(-w * 0.5f, h * 0.5f, 0f); sc = new Vector3(t, h, 0.001f); break; // f
                        default: p = Vector3.zero; sc = new Vector3(w, t, 0.001f); break;                   // g
                    }
                    var seg = CrystalFx.Prim(PrimitiveType.Cube, "Seg" + d + s, disp, new Vector3(cx, 0f, 0f) + p + new Vector3(0f, 0f, 0.0015f), sc, _segOffMat, false);
                    _segs[d * 7 + s] = seg.GetComponent<Renderer>();
                }
            }
            _dispLabel = CrystalFx.Text(disp, "DispLabel", "seconds: 32 768 Hz / 2^15 = 1 Hz", 0.11f, new Color(0.6f, 0.9f, 1f), new Vector3(0f, 0.15f, -0.002f), new Vector2(0.4f, 0.04f));

            _readout = CrystalFx.Text(transform, "Readout", "", 0.13f, new Color(0.55f, 1f, 0.85f), OnTop(0.22f, 0.42f, 0.08f), new Vector2(0.8f, 0.09f));
        }

        void ToggleLamp()
        {
            _lamp.On = !_lamp.On;
            _lampBulb.sharedMaterial = _lamp.On ? _bulbOn : _bulbOff;
            CrystalGuideCard.SetChipLabel(_lampChip, _lamp.On ? "Lamp: ON" : "Lamp: OFF");
            QhysicsSfx.PlayAt(SfxId.CrystalClick, _lamp.transform.position, 0.4f);
        }

        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.deltaTime);

            // Lumped thermal model: the thin cell follows its surroundings with tau ~ 6 s.
            float tEnv = CrystalHeatSource.EnvironmentAt(_analyzerRing != null ? _analyzerRing.position : transform.position, (float)AmbientC, out float coupling);
            _tempC += (tEnv - _tempC) * (1.0 - Math.Exp(-dt * Math.Max(1f, coupling) / 6.0));

            // Director response: on is fast and voltage dependent, off is the elastic relaxation (both ms; shown in the live line).
            double tau = _v > _vEff ? TauOn(Math.Max(_v, 1e-3)) : TauOff;
            if (double.IsInfinity(tau) || tau <= 0.0) _vEff = _v;
            else _vEff += (_v - _vEff) * (1.0 - Math.Exp(-dt / Math.Max(1e-4, tau)));

            // DC drive slowly 'damages' the cell (ions pile up at the electrodes). AC does not.
            if (_dc && _v > 1.0)
                _ionDamage = Math.Min(0.7, _ionDamage + dt * 0.05 * (_v / 3.0));

            double tOn = TNow, t0 = T0;
            SetLum(_cellMat, tOn);
            // Display: undriven glass/segments = T(0); driven segments = T(V).
            SetLum(_segOffMat, t0);
            SetLum(_segOnMat, Transmission(_vEff));
            int sec = _clock ? (int)(Time.time % 60f) : 88;
            if (sec != _shown)
            {
                _shown = sec;
                int tens = (sec / 10) % 10, ones = sec % 10;
                for (int s = 0; s < 7; s++)
                {
                    _segs[s].sharedMaterial = (Digit[tens] & (1 << s)) != 0 ? _segOnMat : _segOffMat;
                    _segs[7 + s].sharedMaterial = (Digit[ones] & (1 << s)) != 0 ? _segOnMat : _segOffMat;
                }
                if (_clock && _v > 0.5)
                    QhysicsSfx.PlayAt(SfxId.CrystalClick, _segs[0].transform.position, 0.06f, 1.6f);
            }
            if (_analyzerRing != null)
                _analyzerRing.localRotation = Quaternion.Euler(0f, 0f, -(float)_analyzerDeg);

            // Goals: crossed polarisers only for the switch / melt goals.
            bool crossed = Math.Abs(_analyzerDeg - 90.0) < 10.0;
            if (crossed && !Isotropic && tOn < 0.1 && _v <= 3.0 && _ionDamage < 0.3)
                CrystalGoals.Latch("lcd_switch");
            if (!Isotropic && Math.Abs(_v - Vth) <= 0.06)
                CrystalGoals.Latch("lcd_threshold");
            if (crossed && Isotropic && _v < 0.05 && t0 < 0.1)
                CrystalGoals.Latch("lcd_melt");

            if (Cell != null)
            {
                double vth = Vth;
                float tilt = Isotropic ? 0f : (float)Math.Max(0.0, Math.Min(1.0, (_vEff - vth) / (1.5 * vth)));
                Cell.Drive = _vEff > vth ? Mathf.Sqrt(tilt) : 0f;
                Cell.Aux = Isotropic ? 1f : (float)(0.25 * (1.0 - S / 0.85));
                Cell.SetCaption(Isotropic ? "above 35 C: isotropic liquid\nno order, no twist, no guiding"
                    : _vEff > vth ? "field > threshold: rods stand up\ntwist lost -> light not rotated" : "below threshold: 90 deg twist\nguides the light's polarisation");
            }

            _tick += Time.deltaTime;
            if (_tick > 0.15f && _readout != null)
            {
                _tick = 0f;
                _readout.text = "V " + _v.ToString("0.00") + " Vrms" + (_dc ? " DC" : " AC") + "   Vth " + (Isotropic ? "--" : Vth.ToString("0.00") + " V")
                    + "   T(cell) " + (tOn * 100.0).ToString("0") + "%   analyser " + _analyzerDeg.ToString("0") + " deg   " + _tempC.ToString("0.0") + " C"
                    + (Isotropic ? "  ISOTROPIC" : "") + (_ionDamage > 0.05 ? "  <color=#FFB050>ion damage " + (_ionDamage * 100).ToString("0") + "%</color>" : "");
            }
        }

        void SetLum(Material m, double t)
        {
            float k = Mathf.Clamp01((float)t);
            Color c = new Color(0.95f, 0.98f, 1f) * k;
            Color baseC = Color.Lerp(new Color(0.02f, 0.025f, 0.03f), new Color(0.9f, 0.95f, 1f), k);
            baseC.a = 1f;
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, baseC);
            if (m.HasProperty(EmissionId)) m.SetColor(EmissionId, c * 1.3f);
        }

        protected override CrystalGuideCard.Content GetContent()
        {
            return new CrystalGuideCard.Content
            {
                Title = "8  LIQUID CRYSTALS  - the twisted cell inside every LCD",
                ChallengeId = ChallengeId,
                What =
                    "A liquid crystal flows like a liquid but its rod-shaped molecules line up like a crystal. Between two rubbed glass plates they form a gentle 90 degree twist.\n\n"
                    + "Light from the backlight passes the first polariser, then the twist carries its polarisation round by 90 degrees, so it slips through the crossed second polariser: the cell looks WHITE.\n\n"
                    + "Put about 1 V across the cell and the molecules stand up along the field. The twist is gone, the light is no longer rotated, and the crossed polariser blocks it: BLACK. That is one pixel. The watch digits on the right are seven of these each.\n\n"
                    + "Heat it past 35 C and the order melts into an ordinary liquid: the whole display goes dark.\n\n"
                    + "<color=#9FB4C4>Model: textbook formulas for threshold, guiding and timing; the curve above threshold is a fitted shape, not a full director solve.</color>",
                Math =
                    "<b>Threshold (TN)</b>   V_th = pi sqrt( (K11 + (K33 - 2 K22)/4) / (eps0 d_eps) ) = " + VthAt25.ToString("0.00") + " V   (no gap dependence!)\n"
                    + "<b>Guiding (Gooch-Tarry)</b>   leak = sin^2(pi/2 sqrt(1+u^2)) / (1+u^2),   u = 2 d dn / lambda   (u = sqrt3 at d ~ 2.6 um: 1st minimum)\n"
                    + "<b>Order (Haller)</b>   S = S0 (1 - T/T*)^0.2,   dn ~ S,  K ~ S^2  ->  V_th ~ sqrt(S)\n"
                    + "<b>Speed</b>   tau_off = g1 d^2 / (K pi^2) ~ 27 ms at 5 um;   tau_on = g1 d^2 / (eps0 d_eps (V^2 - V_th^2))\n"
                    + "<b>Transmission</b>   T = R sin^2(phi) + (1-R) cos^2(phi),  R = guided fraction, phi = analyser angle\n\n"
                    + "5CB: dn = 0.18 (589 nm), d_eps = +11.5, K11/K22/K33 = 6.4/3.0/10 pN, g1 = 0.08 Pa s, T_NI = 35.3 C  [Gray et al. Electron. Lett. 9, 130 (1973); Bradshaw et al. J. Physique 46, 1513 (1985); Schadt & Helfrich APL 18, 127 (1971); Gooch & Tarry J. Phys. D 8, 1575 (1975)]",
                TryThis =
                    "1. Look through the purple analyser ring: white. Turn the cyan voltage knob (or V chips) slowly up from 0.\n"
                    + "2. Nothing happens until ~0.85 V, then the cell darkens fast. That knee is the Freedericksz threshold.\n"
                    + "3. The digits on the right show the seconds: driven segments are the dark ones. Below threshold they vanish.\n"
                    + "4. Twist the analyser to 0 deg (parallel): the picture inverts (normally-black mode).\n"
                    + "5. Heat lamp ON and wait: at 35.3 C the whole screen goes black. Lamp off: it comes back.\n"
                    + "6. Switch to DC and leave it: ions migrate and the contrast fades. Real displays always use AC.",
                Lattice =
                    "The model shows rods in five layers between the plates. The rubbed glass pins the bottom layer along 0 deg and the top along 90 deg, so the layers between twist like a spiral staircase (model).\n\n"
                    + "5CB molecules have a strong dipole along their length (positive d_eps), so the field turns them to stand up. The wall layers stay pinned, which is why a threshold exists: the field has to beat the elastic twist and bend energy, and both scale the same way with gap, so V_th does not depend on d.\n\n"
                    + "Heating makes the rods wobble (the order parameter S falls). Above T_NI they point randomly: no birefringence, no guiding."
            };
        }

        protected override void AddControls(CrystalGuideCard card)
        {
            card.AddControl("V +0.05", () => _vDial.Set(_vDial.Value01 + 0.05f / VMax));
            card.AddControl("V -0.05", () => _vDial.Set(_vDial.Value01 - 0.05f / VMax));
            card.AddControl("V +0.5", () => _vDial.Set(_vDial.Value01 + 0.5f / VMax));
            card.AddControl("V 0 / 3 V", () => _vDial.Set(_v > 0.01 ? 0f : 3f / VMax));
            card.AddControl("Analyser 90/0", () => _aDial.Set(Math.Abs(_analyzerDeg - 90.0) < 1.0 ? 0f : 0.5f));
            _lampChip = card.AddControl("Lamp: OFF", ToggleLamp);
            _gapChip = card.AddControl("Gap: 5 um", () =>
            {
                _gap = _gap > 4e-6 ? 2.65e-6 : (_gap > 2e-6 ? 1.5e-6 : 5e-6);
                CrystalGuideCard.SetChipLabel(_gapChip, "Gap: " + (_gap * 1e6).ToString("0.##") + " um");
            });
            _driveChip = card.AddControl("Drive: AC", () =>
            {
                _dc = !_dc;
                CrystalGuideCard.SetChipLabel(_driveChip, _dc ? "Drive: DC" : "Drive: AC");
            });
            card.AddControl("Fresh cell", () => { _ionDamage = 0; QhysicsSfx.Play2D(SfxId.CrystalClick, 0.4f); });
            _clockChip = card.AddControl("Clock: ON", () =>
            {
                _clock = !_clock;
                _shown = -1;
                CrystalGuideCard.SetChipLabel(_clockChip, _clock ? "Clock: ON" : "Show: 88");
            });
        }

        protected override string LiveLine()
        {
            string tauOn = double.IsInfinity(TauOn(Math.Max(_v, 1e-3))) ? "--" : (TauOn(Math.Max(_v, 1e-3)) * 1e3).ToString("0.0") + " ms";
            return "V " + _v.ToString("0.00") + " Vrms   Vth " + (Isotropic ? "-- (isotropic)" : Vth.ToString("0.00") + " V") + "   T " + (TNow * 100).ToString("0") + "%   analyser "
                + _analyzerDeg.ToString("0") + " deg   d " + (_gap * 1e6).ToString("0.##") + " um\n"
                + "cell " + _tempC.ToString("0.0") + " C   S " + S.ToString("0.00") + "   dn " + Dn.ToString("0.000") + "   u " + U.ToString("0.00") + "   leak " + (Leak * 100).ToString("0.0")
                + "%   tau_off " + (TauOff * 1e3).ToString("0") + " ms   tau_on " + tauOn;
        }
    }
}
