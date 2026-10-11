using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using RealityEngine.Audio;
using C = RealityEngine.Crystals.CrystalConstants;

namespace RealityEngine.Crystals
{
    /// <summary>
    /// Station 3: the quartz watch crystal. A tiny quartz tuning fork (here magnified ~40x) is kept ringing by
    /// the converse piezo effect in a Pierce oscillator; 15 divide-by-2 stages turn 32,768 Hz into 1 tick per second.
    /// Physics: cantilever flexural mode f = (beta1^2 / 2 pi) sqrt(E / 12 rho) w / L^2 (Euler-Bernoulli),
    /// mass loading f = f0 sqrt(m_eff / (m_eff + m_gold)) with m_eff = 0.2427 x tine mass (Rayleigh),
    /// fork temperature curve df/f = beta (T - 25)^2, AT-cut thickness-shear f = N / t with a cubic T curve.
    /// Honesty: single-beam model of each tine (no fork coupling / electrode mass / drive level effects);
    /// the AT-cut cubic uses one representative coefficient. Audio is the real frequency divided by 2^7 (and
    /// 2^k for the AT cut) - the actual oscillation is ultrasonic.
    /// </summary>
    public sealed class ResonatorStation : CrystalStation
    {
        public override string Id => "resonator";
        public override string Title => "Quartz Resonator";
        public override string ChallengeId => "crystal_watch";
        protected override CrystalUnitCell.Mode CellMode => CrystalUnitCell.Mode.Resonator;
        protected override string CellTitle => "tine bending: + / - charge on the sides";

        public const double Target = 32768.0;
        const double TineW = 0.25e-3, TineThick = 0.1e-3;
        double _tineL = 2.55e-3;      // m
        double _gold = 2600.0;        // ng of gold on the tine tips
        double _tempC = 25.0;
        bool _atCut;
        double _atThick = 0.1661e-3;  // 10 MHz AT-cut blank
        bool _power = true;
        bool _beats = true;
        bool _tunedOnce;

        Transform _tineA, _tineB, _secondHand;
        readonly List<Renderer> _leds = new List<Renderer>();
        Material _ledOn, _ledOff;
        TextMeshPro _readout;
        CrystalHum _hum, _ref;
        CrystalDial _tempDial;
        double _phase;
        double[] _ledPhase = new double[15];
        float _tickAcc, _secAcc;
        Transform _goldA, _goldB;
        UnityEngine.UI.Button _cutChip, _powChip;

        /// <summary>Unloaded flexural frequency of one tine (Hz).</summary>
        public double BareHz => C.Beta1Sq / (2.0 * Math.PI) * Math.Sqrt(C.QuartzE / (12.0 * C.QuartzRho)) * TineW / (_tineL * _tineL);
        /// <summary>Effective (modal) tine mass in ng.</summary>
        public double MeffNg => 0.2427 * C.QuartzRho * _tineL * TineW * TineThick * 1e12;
        public double FreqAt25 => _atCut ? C.AtCutN / _atThick : BareHz * Math.Sqrt(MeffNg / (MeffNg + _gold));
        public double TempPpm => _atCut ? C.AtCutC3 * 1e6 * Math.Pow(_tempC - 26.0, 3) : C.ForkBeta * 1e6 * (_tempC - 25.0) * (_tempC - 25.0);
        public double Freq => FreqAt25 * (1.0 + TempPpm * 1e-6);
        public double NominalHz => _atCut ? 10e6 : Target;
        public double ErrorPpm => (Freq / NominalHz - 1.0) * 1e6;
        public double SecPerDay => ErrorPpm * 1e-6 * 86400.0;
        public int AudibleDivide => _atCut ? 15 : 7;

        protected override void BuildStation()
        {
            Material dark = CrystalFx.Solid(new Color(0.12f, 0.13f, 0.15f), 0.6f, 0.5f);
            Material metal = CrystalFx.Solid(new Color(0.75f, 0.77f, 0.8f), 0.9f, 0.7f);
            Material quartz = CrystalFx.Crystal(new Color(0.88f, 0.94f, 1f, 0.55f), new Color(0.05f, 0.1f, 0.14f));
            Material gold = CrystalFx.Solid(new Color(1f, 0.78f, 0.3f), 1f, 0.8f, new Color(0.12f, 0.08f, 0.01f));
            Material electrode = CrystalFx.Solid(new Color(0.85f, 0.85f, 0.88f), 1f, 0.75f);

            // Magnified fork on a can base (left-centre). Real fork: tines 2.55 x 0.25 x 0.1 mm.
            var fork = new GameObject("WatchFork").transform;
            fork.SetParent(transform, false);
            fork.localPosition = OnTop(-0.45f, 0f, 0.05f);
            CrystalFx.Prim(PrimitiveType.Cylinder, "CanBase", fork, new Vector3(0f, 0.015f, 0f), new Vector3(0.14f, 0.015f, 0.14f), metal);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Lead1", fork, new Vector3(-0.025f, -0.0f, 0f), new Vector3(0.005f, 0.02f, 0.005f), metal, false);
            CrystalFx.Prim(PrimitiveType.Cube, "ForkStem", fork, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.06f, 0.012f), quartz);
            _tineA = Tine(fork, -0.0175f, quartz, electrode, out _goldA, gold);
            _tineB = Tine(fork, 0.0175f, quartz, electrode, out _goldB, gold);
            CrystalFx.Text(fork, "ForkLabel", "watch crystal x40\n(tines 2.55 x 0.25 mm)", 0.14f, new Color(0.6f, 0.9f, 1f, 0.9f), new Vector3(0f, 0.27f, 0f), new Vector2(0.34f, 0.08f));
            _hum = CrystalHum.Attach(fork.gameObject);
            var refGo = new GameObject("ReferenceTone");
            refGo.transform.SetParent(transform, false);
            refGo.transform.localPosition = OnTop(0.3f, 0.1f, 0.1f);
            _ref = CrystalHum.Attach(refGo);

            // Divider chain: 15 flip-flop LEDs (left to right = /2 ... /32768).
            _ledOn = CrystalFx.Emissive(new Color(0.1f, 1f, 0.6f), 1.8f);
            _ledOff = CrystalFx.Solid(new Color(0.05f, 0.12f, 0.08f), 0f, 0.6f);
            CrystalFx.Prim(PrimitiveType.Cube, "DividerBoard", transform, OnTop(0.12f, 0.01f, 0.18f), new Vector3(0.6f, 0.02f, 0.12f), CrystalFx.Solid(new Color(0.05f, 0.25f, 0.12f), 0f, 0.5f));
            for (int i = 0; i < 15; i++)
            {
                var led = CrystalFx.Prim(PrimitiveType.Cube, "Led" + (i + 1), transform, OnTop(-0.154f + i * 0.039f, 0.026f, 0.16f), new Vector3(0.022f, 0.012f, 0.022f), _ledOff, false);
                _leds.Add(led.GetComponent<Renderer>());
            }
            CrystalFx.Text(transform, "DivLabel", "15 x divide-by-2:  32768 Hz  ->  1 Hz", 0.15f, new Color(0.6f, 0.9f, 1f, 0.9f), OnTop(0.12f, 0.045f, 0.12f), new Vector2(0.6f, 0.04f));

            // Watch face (right) with a seconds hand driven by the last divider stage.
            var face = new GameObject("WatchFace").transform;
            face.SetParent(transform, false);
            face.localPosition = OnTop(0.62f, 0.15f, 0.12f);
            var dial = CrystalFx.Prim(PrimitiveType.Cylinder, "Face", face, Vector3.zero, new Vector3(0.22f, 0.006f, 0.22f), CrystalFx.Solid(new Color(0.9f, 0.92f, 0.95f), 0f, 0.6f));
            dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CrystalFx.Prim(PrimitiveType.Cube, "Stand", face, new Vector3(0f, -0.08f, 0.01f), new Vector3(0.03f, 0.14f, 0.02f), dark);
            for (int i = 0; i < 12; i++)
            {
                var m = CrystalFx.Prim(PrimitiveType.Cube, "Mark" + i, face, Quaternion.Euler(0f, 0f, -i * 30f) * new Vector3(0f, 0.095f, -0.007f), new Vector3(0.004f, 0.016f, 0.002f), dark, false);
                m.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
            }
            var hp = new GameObject("SecondHand").transform;
            hp.SetParent(face, false);
            hp.localPosition = new Vector3(0f, 0f, -0.009f);
            CrystalFx.Prim(PrimitiveType.Cube, "Hand", hp, new Vector3(0f, 0.04f, 0f), new Vector3(0.003f, 0.1f, 0.002f), CrystalFx.Solid(new Color(0.9f, 0.15f, 0.1f)), false);
            _secondHand = hp;

            // Temperature knob + readout (front centre).
            var tk = CrystalFx.Prim(PrimitiveType.Cylinder, "TempKnob", transform, OnTop(-0.1f, 0.02f, -0.15f), new Vector3(0.07f, 0.02f, 0.07f), CrystalFx.Emissive(new Color(1f, 0.45f, 0.15f), 0.6f));
            _tempDial = tk.AddComponent<CrystalDial>();
            _tempDial.Label = "Temperature";
            _tempDial.TurnsForFull = 1f;
            _tempDial.DesktopStep = 1f / 14f;
            _tempDial.Value01 = (25f + 10f) / 70f;
            _tempDial.Changed = v => { _tempC = -10.0 + 70.0 * v; tk.transform.localRotation = Quaternion.Euler(0f, v * 300f, 0f); CheckTuned(); };
            CrystalFx.Text(transform, "TempLabel", "OVEN  -10..60 C", 0.13f, new Color(1f, 0.7f, 0.4f), OnTop(-0.1f, 0.06f, -0.15f), new Vector2(0.2f, 0.03f));
            _readout = CrystalFx.Text(transform, "Readout", "", 0.17f, new Color(0.55f, 1f, 0.85f), OnTop(0.2f, 0.12f, 0.02f), new Vector2(0.42f, 0.22f), TextAlignmentOptions.TopLeft);
            CrystalFx.Prim(PrimitiveType.Cube, "ReadoutPanel", transform, OnTop(0.2f, 0.11f, 0.05f), new Vector3(0.44f, 0.22f, 0.04f), dark);
        }

        Transform Tine(Transform fork, float x, Material q, Material e, out Transform goldDot, Material gold)
        {
            var pivot = new GameObject("TinePivot").transform;
            pivot.SetParent(fork, false);
            pivot.localPosition = new Vector3(x, 0.12f, 0f);
            CrystalFx.Prim(PrimitiveType.Cube, "Tine", pivot, new Vector3(0f, 0.051f, 0f), new Vector3(0.01f, 0.102f, 0.004f), q, false);
            CrystalFx.Prim(PrimitiveType.Cube, "Electrode", pivot, new Vector3(0f, 0.04f, -0.0022f), new Vector3(0.006f, 0.07f, 0.0005f), e, false);
            goldDot = CrystalFx.Prim(PrimitiveType.Cube, "GoldLoad", pivot, new Vector3(0f, 0.094f, 0f), new Vector3(0.011f, 0.012f, 0.005f), gold, false).transform;
            return pivot;
        }

        void Trim(double ng)
        {
            double before = _gold;
            _gold = Math.Max(0.0, _gold + ng);
            if (Math.Abs(_gold - before) < 1e-9)
                return;
            if (ng < 0)
            {
                Vector3 at = _goldA.position;
                CrystalFx.Flash(at, new Color(0.4f, 1f, 0.5f), 0.6f, 0.08f, 0.012f);
                QhysicsSfx.PlayAt(SfxId.CrystalClick, at, 0.35f, 1.8f);
            }
            else
            {
                QhysicsSfx.PlayAt(SfxId.UiClick, _goldA.position, 0.35f, 0.8f);
            }
            GoldVisual();
            CheckTuned();
        }

        void GoldVisual()
        {
            float gs = (float)(_gold / 2600.0);
            _goldA.localScale = _goldB.localScale = new Vector3(0.011f, 0.002f + 0.01f * Mathf.Clamp(gs, 0f, 1.6f), 0.005f);
        }

        void CheckTuned()
        {
            if (!_atCut && Math.Abs(ErrorPpm) <= 10.0)
            {
                CrystalGoals.Latch("watch_tuned");
                if (!_tunedOnce)
                {
                    _tunedOnce = true;
                    QhysicsSfx.PlayAt(SfxId.CrystalRing, _goldA.position, 0.6f);
                    CrystalFx.PulseBoth(0.25f, 0.08f);
                }
            }
        }

        void Update()
        {
            double f = Freq;
            float dt = Time.deltaTime;
            // Visual tine swing: real 32 kHz shown at a strobe-like 3 Hz.
            float amp = _power ? 1f : 0f;
            _phase += dt * 3.0;
            float bend = Mathf.Sin((float)(_phase * 2.0 * Math.PI)) * amp * (_atCut ? 0f : 4f);
            _tineA.localRotation = Quaternion.Euler(0f, 0f, bend);
            _tineB.localRotation = Quaternion.Euler(0f, 0f, -bend);

            // Divider LEDs: stage k toggles at f / 2^k. Fast stages are shown as a steady half-bright blur.
            double fd = _atCut ? f / 305.17578125 : f; // AT: show the chain after a /305.17578125 prescaler (10 MHz -> 32768 Hz)
            for (int k = 0; k < 15; k++)
            {
                double fk = fd / Math.Pow(2.0, k + 1);
                bool on;
                if (!_power) on = false;
                else if (fk > 12.0) on = ((int)(Time.time * 30f) + k) % 2 == 0;
                else
                {
                    _ledPhase[k] += dt * fk;
                    on = (_ledPhase[k] % 1.0) < 0.5;
                }
                Material want = on ? _ledOn : _ledOff;
                if (_leds[k].sharedMaterial != want)
                    _leds[k].sharedMaterial = want;
            }
            if (_power)
            {
                _secAcc += dt * (float)(fd / Target);
                while (_secAcc >= 1f)
                {
                    _secAcc -= 1f;
                    _secondHand.localRotation *= Quaternion.Euler(0f, 0f, -6f);
                    QhysicsSfx.PlayAt(SfxId.UiTick, _secondHand.position, 0.12f, 1.6f);
                }
            }

            // Sound: oscillator divided down (f / 2^7 = 256 Hz when perfect) + optional 256 Hz reference -> beats.
            double heard = f / Math.Pow(2.0, AudibleDivide);
            double refHz = NominalHz / Math.Pow(2.0, AudibleDivide);
            if (_hum != null) _hum.SetTone((float)heard, _power ? 0.22f : 0f);
            if (_ref != null) _ref.SetTone((float)refHz, _power && _beats ? 0.18f : 0f);

            if (Cell != null)
            {
                Cell.Drive = _power ? 1f : 0f;
                Cell.Aux = (float)(_phase % 1.0);
                Cell.SetCaption(_atCut ? "AT-cut: faces shear in\nopposite directions" : "bending squeezes one side,\nstretches the other");
            }

            _tickAcc += dt;
            if (_tickAcc > 0.15f && _readout != null)
            {
                _tickAcc = 0f;
                double err = ErrorPpm;
                string col = Math.Abs(err) <= 10 ? "#7CFF9A" : (Math.Abs(err) <= 100 ? "#FFD24A" : "#FF6A5A");
                _readout.text = "<b>" + (_atCut ? "AT-CUT 10 MHz" : "FORK 32768 Hz") + "</b>   " + _tempC.ToString("0") + " C\n"
                    + "f = " + f.ToString(_atCut ? "0.0" : "0.000") + " Hz\n"
                    + "<color=" + col + ">error " + err.ToString("+0.0;-0.0") + " ppm = " + SecPerDay.ToString("+0.00;-0.00") + " s/day</color>\n"
                    + (_atCut ? "blank t = " + (_atThick * 1e3).ToString("0.0000") + " mm" : "gold " + _gold.ToString("0.0") + " ng   L " + (_tineL * 1e3).ToString("0.000") + " mm")
                    + (_beats ? "\nbeat " + Math.Abs(heard - refHz).ToString("0.000") + " Hz" : "");
            }
        }

        protected override CrystalGuideCard.Content GetContent()
        {
            return new CrystalGuideCard.Content
            {
                Title = "3  QUARTZ RESONATOR  - why your watch keeps time",
                ChallengeId = ChallengeId,
                What =
                    "Inside almost every watch is a tiny quartz tuning fork, 3 mm long. Quartz is piezoelectric, so a voltage bends it (converse effect) "
                    + "and bending it makes a voltage (direct effect). An amplifier feeds the fork's own voltage back to it, so it rings at exactly its natural frequency: 32,768 Hz.\n\n"
                    + "Why 32,768? It is 2^15: fifteen divide-by-two circuits turn it into exactly one tick per second (watch the green LEDs ripple).\n\n"
                    + "Factories make the tines a little too fast, coat the tips with gold, then laser off tiny amounts until it is right. Your job now.\n\n"
                    + "<color=#9FB4C4>Model: single-beam tine, Rayleigh tip mass; sound = frequency / 128 (real tone is ultrasonic).</color>",
                Math =
                    "<b>Tine (cantilever)</b>   f0 = (3.516 / 2 pi) sqrt(E / 12 rho) w / L^2\n"
                    + "   E = 78 GPa, rho = 2648 kg/m3, w = 0.25 mm, L = 2.55 mm -> f0 ~ 33.7 kHz\n"
                    + "<b>Gold loading</b>   f = f0 sqrt(m_eff / (m_eff + m)),  m_eff = 0.243 x tine mass ~ 41,000 ng\n"
                    + "   1 ng of gold ~ 0.38 Hz ~ 12 ppm\n"
                    + "<b>Temperature (fork)</b>   df/f = -0.034 ppm/K^2 (T - 25 C)^2\n"
                    + "<b>AT-cut plate</b>   f = 1661 Hz m / t;  df/f ~ 1.09e-4 ppm/K^3 (T - 26 C)^3\n"
                    + "<b>Clock error</b>   s/day = ppm x 0.0864\n\n"
                    + "[Bechmann 1958 / IEEE 176; Euler-Bernoulli beam; Rayleigh; Citizen/Epson CFS-206 & Micro Crystal datasheets; Vig, Quartz Crystal Resonators tutorial]",
                TryThis =
                    "1. Listen: the fork hum (f/128) and a perfect 256 Hz reference play together. Out of tune you hear slow BEATS (wah-wah).\n"
                    + "2. Trim gold: -100 ng until the error is under ~300 ppm, then -10, -1, and -0.1 ng. Too far? +1 ng adds gold back.\n"
                    + "3. Get within +/-10 ppm (< 1 s/day) - the beats stop and the challenge ticks.\n"
                    + "4. Turn the OVEN knob: hot or cold, the fork slows (parabola). That is why a watch on your wrist (~30 C) is tuned for it.\n"
                    + "5. Switch Cut to AT 10 MHz: the cubic curve stays within a few ppm over a wide range - that is why radios and computers use AT-cut plates.\n"
                    + "6. Tine L+ / L-: frequency goes as 1/L^2.",
                Lattice =
                    "The fork is cut from a quartz wafer so the electric axis runs across the tines. Electrodes on the sides of each tine apply a field that "
                    + "stretches one side and squeezes the other: the tine bends (model: red/blue show the charge on each side while it flexes).\n\n"
                    + "The two tines swing in opposite directions, so the stem barely moves and almost no energy leaks into the mount: Q of ~50,000 or more in vacuum.\n\n"
                    + "The AT-cut is a slice at 35.25 deg to the optic axis. At that angle two temperature effects of the lattice cancel, flattening the curve."
            };
        }

        protected override void AddControls(CrystalGuideCard card)
        {
            card.AddControl("Trim -100 ng", () => Trim(-100));
            card.AddControl("Trim -10 ng", () => Trim(-10));
            card.AddControl("Trim -1 ng", () => Trim(-1));
            card.AddControl("Trim -0.1 ng", () => Trim(-0.1));
            card.AddControl("Add +1 ng", () => Trim(1));
            card.AddControl("New blank", () => { _gold = 2600; _tineL = 2.55e-3; _atThick = 0.1661e-3; _tunedOnce = false; GoldVisual(); });
            _cutChip = card.AddControl("Cut: Fork", () =>
            {
                _atCut = !_atCut;
                CrystalGuideCard.SetChipLabel(_cutChip, _atCut ? "Cut: AT 10MHz" : "Cut: Fork");
            });
            card.AddControl("Tine L+", () => { if (_atCut) _atThick *= 1.001; else _tineL += 0.01e-3; CheckTuned(); });
            card.AddControl("Tine L-", () => { if (_atCut) _atThick /= 1.001; else _tineL = Math.Max(1.5e-3, _tineL - 0.01e-3); CheckTuned(); });
            card.AddControl("Oven 25 C", () => _tempDial.Set(35f / 70f));
            card.AddControl("Beats on/off", () => _beats = !_beats);
            _powChip = card.AddControl("Power: ON", () =>
            {
                _power = !_power;
                CrystalGuideCard.SetChipLabel(_powChip, _power ? "Power: ON" : "Power: OFF");
            });
        }

        protected override string LiveLine()
        {
            if (_atCut)
                return "AT-cut thickness-shear: f = 1661 / t = " + CrystalFx.FormatHz(FreqAt25) + " at 26 C; at " + _tempC.ToString("0") + " C: "
                    + TempPpm.ToString("+0.00;-0.00") + " ppm  -> " + SecPerDay.ToString("+0.00;-0.00") + " s/day";
            return "f0(bare) " + BareHz.ToString("0") + " Hz   m_eff " + MeffNg.ToString("0") + " ng   gold " + _gold.ToString("0.0") + " ng   f(25C) "
                + FreqAt25.ToString("0.000") + " Hz   temp " + TempPpm.ToString("+0.0;-0.0") + " ppm\nerror " + ErrorPpm.ToString("+0.0;-0.0")
                + " ppm (" + SecPerDay.ToString("+0.00;-0.00") + " s/day)   target +/-10 ppm   heard f/128 = " + (Freq / 128.0).ToString("0.00") + " Hz vs 256";
        }
    }
}
