using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.Audio;
using C = RealityEngine.Crystals.CrystalConstants;

namespace RealityEngine.Crystals
{
    /// <summary>
    /// Station 7: ferroelectric hysteresis and the Curie point in barium titanate (BaTiO3).
    /// Model: Landau-Khalatnikov dynamics in reduced units, dp/dt = -k (a p + p^3 - e), a = (T - Tc) / 100 K,
    /// so below Tc the spontaneous polarisation is p0 = sqrt(-a) (p = 1 -> Ps = 0.26 C/m^2 at 25 C) and the
    /// coercive field is e_c = 2 / (3 sqrt 3) (-a)^1.5. Field scale: e = 1 -> 2.6 kV/mm (gives Ec ~ 1 kV/mm at 25 C).
    /// Above Tc the loop collapses to a straight (paraelectric) line with slope 1/a (Curie-Weiss).
    /// Honesty: single-domain mean-field (Landau 2-4) model; real ceramics switch by domain-wall motion at lower
    /// fields, BaTiO3's transition is first order (small jump), and we ignore the lower-T phase changes.
    /// </summary>
    public sealed class FerroStation : CrystalStation
    {
        public override string Id => "ferro";
        public override string Title => "Ferroelectric + Curie";
        public override string ChallengeId => "crystal_ferro";
        protected override CrystalUnitCell.Mode CellMode => CrystalUnitCell.Mode.Perovskite;
        protected override string CellTitle => "BaTiO3: Ti(+) in an O(-) octahedron";

        public const double FieldScaleKvMm = 2.6;
        const double K = 25.0;          // 1/s relaxation (tuned so the loop follows a 0.4 Hz drive)
        const double DriveHz = 0.4;
        const int Trail = 260;

        double _p = 1.0;
        double _e;
        double _tempC = 25.0;
        double _amp = 0.9;
        double _phase;
        bool _drive = true;
        LineRenderer _loop;
        readonly Vector3[] _pts = new Vector3[Trail];
        int _count, _head;
        Transform _dot, _plot;
        readonly List<Renderer> _domains = new List<Renderer>();
        readonly List<float> _domainBias = new List<float>();
        Material _up, _down, _para;
        TextMeshPro _readout;
        Transform _heaterGlow;
        Renderer _heaterR;
        Material _hOn, _hOff;
        CrystalDial _tDial;
        float _tick;
        double _loopArea, _prevE;
        double _cycleArea;
        double _remanent;
        bool _sawLoopBelow;
        UnityEngine.UI.Button _ampChip, _driveChip;

        double A => (_tempC - C.BaTiO3CurieC) / 100.0;
        public double Polarisation => _p * C.BaTiO3Ps;
        public double FieldKvMm => _e * FieldScaleKvMm;
        public double CoerciveKvMm => A < 0 ? 2.0 / (3.0 * Math.Sqrt(3.0)) * Math.Pow(-A, 1.5) * FieldScaleKvMm : 0.0;
        /// <summary>Curie-Weiss above Tc: epsR ~ C / (T - T0), C ~ 1.7e5 K (BaTiO3), T0 ~ Tc - 10 K.</summary>
        public double CurieWeissEps => _tempC > C.BaTiO3CurieC - 5 ? 1.7e5 / Math.Max(2.0, _tempC - (C.BaTiO3CurieC - 10.0)) : 0.0;

        protected override void BuildStation()
        {
            Material dark = CrystalFx.Solid(new Color(0.12f, 0.13f, 0.15f), 0.6f, 0.5f);
            Material steel = CrystalFx.Solid(new Color(0.55f, 0.58f, 0.62f), 0.85f, 0.55f);
            _up = CrystalFx.Solid(new Color(0.95f, 0.32f, 0.28f), 0f, 0.6f, new Color(0.3f, 0.04f, 0.03f));
            _down = CrystalFx.Solid(new Color(0.25f, 0.5f, 1f), 0f, 0.6f, new Color(0.03f, 0.08f, 0.3f));
            _para = CrystalFx.Solid(new Color(0.6f, 0.6f, 0.62f), 0f, 0.5f);

            // Sample: ceramic disc between electrodes on a hot plate, with a 6 x 6 domain mosaic on top.
            var sample = new GameObject("Sample").transform;
            sample.SetParent(transform, false);
            sample.localPosition = OnTop(-0.45f, 0f, 0.05f);
            _hOff = CrystalFx.Solid(new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.5f);
            _hOn = CrystalFx.Emissive(new Color(1f, 0.3f, 0.05f), 1.4f);
            var hp = CrystalFx.Prim(PrimitiveType.Cube, "HotPlate", sample, new Vector3(0f, 0.025f, 0f), new Vector3(0.26f, 0.05f, 0.26f), dark);
            _heaterR = CrystalFx.Prim(PrimitiveType.Cube, "HeaterTop", sample, new Vector3(0f, 0.051f, 0f), new Vector3(0.22f, 0.002f, 0.22f), _hOff, false).GetComponent<Renderer>();
            CrystalFx.Prim(PrimitiveType.Cube, "ElectrodeLo", sample, new Vector3(0f, 0.055f, 0f), new Vector3(0.17f, 0.004f, 0.17f), steel, false);
            CrystalFx.Prim(PrimitiveType.Cube, "Ceramic", sample, new Vector3(0f, 0.065f, 0f), new Vector3(0.16f, 0.016f, 0.16f), CrystalFx.Solid(new Color(0.85f, 0.83f, 0.78f), 0f, 0.3f));
            for (int i = 0; i < 36; i++)
            {
                int gx = i % 6, gz = i / 6;
                var d = CrystalFx.Prim(PrimitiveType.Cube, "Domain", sample, new Vector3(-0.0625f + gx * 0.025f, 0.075f, -0.0625f + gz * 0.025f), new Vector3(0.022f, 0.004f, 0.022f), _up, false);
                _domains.Add(d.GetComponent<Renderer>());
                _domainBias.Add(Mathf.Sin(i * 12.9898f) * 0.5f);
            }
            CrystalFx.Text(sample, "SampleLabel", "BaTiO3 ceramic\nred = up  blue = down", 0.13f, new Color(0.6f, 0.9f, 1f), new Vector3(0f, 0.16f, 0f), new Vector2(0.3f, 0.06f));
            var knob = CrystalFx.Prim(PrimitiveType.Cylinder, "HeatKnob", sample, new Vector3(0f, 0.025f, -0.14f), new Vector3(0.05f, 0.012f, 0.05f), CrystalFx.Emissive(new Color(1f, 0.45f, 0.15f), 0.6f));
            knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _tDial = knob.AddComponent<CrystalDial>();
            _tDial.Label = "Hot plate";
            _tDial.TurnsForFull = 1f;
            _tDial.DesktopStep = 1f / 14f;
            _tDial.Value01 = (25f - 20f) / 140f;
            _tDial.Changed = v => _tempC = 20.0 + 140.0 * v;

            // P-E plot (oscilloscope-style Sawyer-Tower display).
            _plot = new GameObject("PEPlot").transform;
            _plot.SetParent(transform, false);
            _plot.localPosition = OnTop(0.25f, 0.2f, 0.12f);
            CrystalFx.Prim(PrimitiveType.Cube, "Scope", _plot, new Vector3(0f, 0f, 0.015f), new Vector3(0.56f, 0.38f, 0.03f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "Screen", _plot, new Vector3(0f, 0f, -0.0005f), new Vector3(0.5f, 0.32f, 0.001f), CrystalFx.Line(new Color(0.01f, 0.05f, 0.04f, 1f)), false);
            CrystalFx.Prim(PrimitiveType.Cube, "AxisE", _plot, new Vector3(0f, 0f, -0.002f), new Vector3(0.48f, 0.0015f, 0.001f), CrystalFx.Line(new Color(0.2f, 0.45f, 0.4f, 1f)), false);
            CrystalFx.Prim(PrimitiveType.Cube, "AxisP", _plot, new Vector3(0f, 0f, -0.002f), new Vector3(0.0015f, 0.3f, 0.001f), CrystalFx.Line(new Color(0.2f, 0.45f, 0.4f, 1f)), false);
            CrystalFx.Text(_plot, "LblE", "E (kV/mm)", 0.11f, new Color(0.5f, 0.9f, 0.8f), new Vector3(0.2f, -0.012f, -0.003f), new Vector2(0.12f, 0.03f));
            CrystalFx.Text(_plot, "LblP", "P (C/m2)", 0.11f, new Color(0.5f, 0.9f, 0.8f), new Vector3(0.045f, 0.145f, -0.003f), new Vector2(0.12f, 0.03f));
            var lg = new GameObject("Loop");
            lg.transform.SetParent(_plot, false);
            _loop = lg.AddComponent<LineRenderer>();
            _loop.useWorldSpace = false;
            _loop.widthMultiplier = 0.003f;
            _loop.sharedMaterial = CrystalFx.Line(new Color(0.3f, 1f, 0.6f, 1f));
            _loop.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _loop.positionCount = 0;
            _dot = CrystalFx.Bead("Dot", _plot, Vector3.zero, 0.012f, CrystalFx.Emissive(new Color(1f, 1f, 0.6f), 2f)).transform;
            _readout = CrystalFx.Text(_plot, "Readout", "", 0.12f, new Color(0.55f, 1f, 0.85f), new Vector3(0f, -0.24f, -0.003f), new Vector2(0.56f, 0.09f));
        }

        void Plot(double e, double p)
        {
            // x: +/- 2.5 (reduced) over 0.46 m; y: +/- 1.3 over 0.28 m.
            var v = new Vector3((float)(e / 2.5 * 0.23), (float)(p / 1.3 * 0.14), -0.003f);
            v.x = Mathf.Clamp(v.x, -0.24f, 0.24f);
            v.y = Mathf.Clamp(v.y, -0.15f, 0.15f);
            _pts[_head] = v;
            _head = (_head + 1) % Trail;
            if (_count < Trail) _count++;
            _dot.localPosition = v;
        }

        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            if (_drive)
            {
                _phase += dt * DriveHz;
                _e = _amp * Math.Sin(_phase * 2.0 * Math.PI);
            }
            // Integrate with small sub-steps (stiff cubic).
            int n = 8;
            double h = dt / n;
            double a = A;
            for (int i = 0; i < n; i++)
                _p += -K * (a * _p + _p * _p * _p - _e) * h;

            // Loop area (shoelace) accumulated over a drive cycle -> energy lost per cycle ~ area.
            _cycleArea += 0.5 * (_prevE * _p - _e * _prevP) ;
            if (_drive && _phase >= 1.0)
            {
                _phase -= 1.0;
                _loopArea = Math.Abs(_cycleArea);
                _cycleArea = 0;
                if (_tempC < C.BaTiO3CurieC - 5 && _loopArea > 0.3)
                    _sawLoopBelow = true;
                if (_sawLoopBelow && _tempC > C.BaTiO3CurieC + 2 && _loopArea < 0.08)
                    CrystalGoals.Latch("ferro_curie");
                if (_tempC < C.BaTiO3CurieC && _loopArea > 0.3)
                    CrystalGoals.Latch("ferro_loop");
            }
            if (_prevE < 0 && _e >= 0) _remanent = -_p; // P at E=0 coming from negative side
            if (_prevE > 0 && _e <= 0) _remanent = _p;
            _prevE = _e;
            _prevP = _p;
            Plot(_e, _p);

            if (_count > 1)
            {
                if (_loop.positionCount != _count) _loop.positionCount = _count;
                int start = (_head - _count + Trail) % Trail;
                for (int i = 0; i < _count; i++)
                    _loop.SetPosition(i, _pts[(start + i) % Trail]);
            }

            // Domains: each flips when p passes its bias; above Tc all go grey (no spontaneous polarisation).
            bool para = A > 0;
            for (int i = 0; i < _domains.Count; i++)
            {
                Material want = para && Math.Abs(_p) < 0.25 ? _para : (_p > _domainBias[i] * Math.Max(0.1, Math.Sqrt(Math.Max(0.0, -a))) ? _up : _down);
                if (_domains[i].sharedMaterial != want) _domains[i].sharedMaterial = want;
            }
            Material hm = _tempC > 40 ? _hOn : _hOff;
            if (_heaterR.sharedMaterial != hm) _heaterR.sharedMaterial = hm;

            if (Cell != null)
            {
                Cell.Drive = (float)Math.Max(-1.0, Math.Min(1.0, _p));
                Cell.SetCaption(para ? "above Tc: cubic, Ti centred\n(only the field shifts it)" : "below Tc: Ti sits off-centre\nup or down -> domains");
            }

            _tick += Time.deltaTime;
            if (_tick > 0.15f && _readout != null)
            {
                _tick = 0f;
                _readout.text = "T " + _tempC.ToString("0") + " C (Tc " + C.BaTiO3CurieC.ToString("0") + " C)   E " + FieldKvMm.ToString("+0.00;-0.00") + " kV/mm   P " + Polarisation.ToString("+0.000;-0.000") + " C/m2\n"
                    + (para ? "PARAELECTRIC  epsR ~ " + CurieWeissEps.ToString("0") : "FERROELECTRIC  Ec ~ " + CoerciveKvMm.ToString("0.00") + " kV/mm   Pr ~ " + (Math.Abs(_remanent) * C.BaTiO3Ps).ToString("0.000") + " C/m2");
            }
        }

        double _prevP;

        protected override CrystalGuideCard.Content GetContent()
        {
            return new CrystalGuideCard.Content
            {
                Title = "7  FERROELECTRICS  - crystal memory and the Curie point",
                ChallengeId = ChallengeId,
                What =
                    "Barium titanate is like a magnet, but electric. Each cell has a built-in dipole that can point up or down, and a strong enough field FLIPS it. "
                    + "Remove the field and it stays flipped: memory. (FeRAM chips store bits exactly like this.)\n\n"
                    + "Sweep the field back and forth and plot polarisation P against field E: you get a loop (hysteresis). Its width is the 'coercive field', its height at E = 0 the 'remanent polarisation'.\n\n"
                    + "Heat it past 125 C, its Curie point, and the memory is gone: the cell becomes perfectly cubic, the loop collapses to a line. Cool it and the loop returns.\n\n"
                    + "BaTiO3 is in billions of capacitors (huge epsR) and, poled, it is a piezoelectric too.\n\n"
                    + "<color=#9FB4C4>Model: single-domain Landau theory (smooth 2nd-order version); real switching happens by domain walls at lower fields.</color>",
                Math =
                    "<b>Landau free energy</b>   F = a/2 P^2 + b/4 P^4 - E P,   a = a0 (T - Tc)\n"
                    + "<b>Dynamics (reduced)</b>   dp/dt = -k (a p + p^3 - e),   a = (T - Tc) / 100 K\n"
                    + "<b>Spontaneous P</b>   Ps = sqrt(-a)  (T < Tc)  -> 0.26 C/m2 at 25 C\n"
                    + "<b>Coercive field</b>   e_c = 2 / (3 sqrt 3) (-a)^1.5   (model ~1 kV/mm at 25 C)\n"
                    + "<b>Above Tc (Curie-Weiss)</b>   epsR ~ C / (T - T0),  C ~ 1.7e5 K\n"
                    + "<b>Loss per cycle</b>   W = loop area (integral of E dP)\n\n"
                    + "BaTiO3: Tc = 120-130 C, Ps = 0.26 C/m2  [Merz 1953 Phys. Rev. 91; Jona & Shirane, Ferroelectric Crystals; Devonshire 1949]",
                TryThis =
                    "1. Watch the scope: the green trace draws the P-E loop as the field sweeps (0.4 Hz).\n"
                    + "2. Watch the domain tiles flip red/blue as the dot crosses the steep sides of the loop.\n"
                    + "3. Lower the amplitude below the coercive field: no switching, just a thin wobble.\n"
                    + "4. Turn up the hot plate (knob or T +10 chips). The loop shrinks...\n"
                    + "5. CHALLENGE: see a full loop below Tc, then heat past 125 C until it collapses to a line.\n"
                    + "6. Freeze the drive at E = 0 after a sweep: the P that remains is the memory.",
                Lattice =
                    "BaTiO3 is a perovskite: Ba at the corners, O at the face centres (forming an octahedron), Ti in the middle (model).\n\n"
                    + "Below 125 C the Ti ion is slightly too small for its cage, so it sits off-centre, nudged up or down by ~0.1 Angstrom. That shift is the cell's dipole. "
                    + "Neighbouring cells line up, forming domains.\n\n"
                    + "A strong field drags Ti through the centre to the other side: the dipole flips. Heat makes the lattice expand and the ions jiggle until the centre is the stable spot: "
                    + "above Tc there is no preferred direction, and the crystal is merely very polarisable."
            };
        }

        protected override void AddControls(CrystalGuideCard card)
        {
            card.AddControl("T +10 C", () => _tDial.Set(_tDial.Value01 + 10f / 140f));
            card.AddControl("T -10 C", () => _tDial.Set(_tDial.Value01 - 10f / 140f));
            card.AddControl("T 25 C", () => _tDial.Set(5f / 140f));
            card.AddControl("T 140 C", () => _tDial.Set(120f / 140f));
            _ampChip = card.AddControl("Amp: 2.3 kV/mm", () =>
            {
                _amp = _amp > 1.5 ? 0.3 : (_amp > 0.5 ? 1.8 : 0.9);
                if (_amp < 0.5) _amp = 0.3;
                CrystalGuideCard.SetChipLabel(_ampChip, "Amp: " + (_amp * FieldScaleKvMm).ToString("0.0") + " kV/mm");
            });
            _driveChip = card.AddControl("Drive: ON", () =>
            {
                _drive = !_drive;
                if (!_drive) _e = 0;
                CrystalGuideCard.SetChipLabel(_driveChip, _drive ? "Drive: ON" : "Drive: E = 0");
            });
            card.AddControl("Pole +", () => { _drive = false; _e = 0; _p = Math.Max(0.05, Math.Sqrt(Math.Max(0.0, -A))); CrystalGuideCard.SetChipLabel(_driveChip, "Drive: E = 0"); });
            card.AddControl("Clear trace", () => { _count = 0; _loop.positionCount = 0; });
        }

        protected override string LiveLine()
        {
            return "T " + _tempC.ToString("0") + " C   a = (T - Tc)/100 K = " + A.ToString("+0.00;-0.00") + "   E " + FieldKvMm.ToString("+0.00;-0.00") + " kV/mm   P "
                + Polarisation.ToString("+0.000;-0.000") + " C/m2\n" + (A < 0 ? "Ps " + (Math.Sqrt(-A) * C.BaTiO3Ps).ToString("0.000") + " C/m2   Ec " + CoerciveKvMm.ToString("0.00") + " kV/mm"
                : "paraelectric: P = E / a   epsR ~ " + CurieWeissEps.ToString("0")) + "   loop area " + _loopArea.ToString("0.00") + " (reduced)";
        }
    }
}
