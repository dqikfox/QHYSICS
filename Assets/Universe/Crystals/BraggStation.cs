using System;
using TMPro;
using UnityEngine;
using RealityEngine.Audio;
using C = RealityEngine.Crystals.CrystalConstants;

namespace RealityEngine.Crystals
{
    /// <summary>
    /// Station 9: Bragg diffraction - why opals flash colours and how X-rays measure atom spacings.
    /// Left: an opal (fcc stack of silica spheres) on a tilt stage under a white lamp. Bragg-Snell:
    /// lambda = 2 d111 sqrt(n_eff^2 - sin^2 theta), d111 = sqrt(2/3) D, n_eff^2 = f n_s^2 + (1 - f) n_m^2 (f = 0.74).
    /// Right: a theta-2theta X-ray goniometer with a Cu tube (K-alpha 154.06 pm, K-beta 139.22 pm, optional Ni filter)
    /// and a NaCl / LiF / KCl crystal cut on (100): peaks at 2 d sin theta = lambda for d = a/2, a/4, a/6 [(200), (400), (600)].
    /// A scan button sweeps theta and plots counts vs 2theta.
    /// Honesty: peak heights are set by hand (falling with order), widths are a fixed 0.2 deg instrument profile;
    /// opal brightness uses a weak-contrast (n_s - n_m)^2 scaling; reflected colour is one Bragg line, not a full
    /// photonic-band calculation.
    /// </summary>
    public sealed class BraggStation : CrystalStation
    {
        public override string Id => "bragg";
        public override string Title => "Bragg Diffraction";
        public override string ChallengeId => "crystal_bragg";
        protected override CrystalUnitCell.Mode CellMode => CrystalUnitCell.Mode.Bragg;
        protected override string CellTitle => "Planes of atoms: 2 d sin(theta) = n lambda";

        const float ThMin = 5f, ThMax = 60f;
        const int ScanN = 551;         // 0.1 deg steps of theta
        const double PeakHwhm = 0.2;   // deg (instrument + rocking curve)
        const double Background = 6.0; // cps

        static readonly string[] CrystalNames = { "NaCl", "LiF", "KCl" };
        static readonly double[] CrystalA = { C.NaClA, C.LiFA, C.KClA };
        static readonly double[] OrderHeight = { 1000.0, 280.0, 60.0 }; // cps for (200), (400), (600) with K-alpha, no filter
        static readonly double[] OpalD = { 220e-9, 250e-9, 300e-9 };
        static readonly string[] MediumName = { "air", "water", "ethanol" };
        static readonly double[] MediumN = { 1.0, C.WaterN, C.EthanolN };

        // Opal
        double _opalTilt;  // deg
        int _opalIdx = 1, _medium;
        CrystalDial _tiltDial;
        Transform _opalSlab;
        Material _opalMat;
        LineRenderer _whiteBeam, _colourBeam;
        Transform _opalTop;

        // Goniometer
        double _theta = 10.0;
        int _crystal;
        bool _niFilter;
        bool _scanning;
        CrystalDial _thDial;
        Transform _sample, _arm, _detector, _bar, _goCentre, _tube;
        LineRenderer _xIn, _xOut, _plotLine;
        readonly float[] _scan = new float[ScanN];
        bool _plotDirty;
        float _nextPlot;
        double _cps;
        float _clickAcc;
        TextMeshPro _gonioText, _opalText;
        float _tick;
        UnityEngine.UI.Button _crystalChip, _filterChip, _scanChip, _opalChip, _mediumChip;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        // ── Physics ──

        public double OpalD111 => Math.Sqrt(2.0 / 3.0) * OpalD[_opalIdx];
        public double OpalNeff2 => C.OpalFill * C.SilicaN * C.SilicaN + (1.0 - C.OpalFill) * MediumN[_medium] * MediumN[_medium];
        /// <summary>Bragg-Snell peak wavelength (m) at the current tilt.</summary>
        public double OpalLambda
        {
            get
            {
                double s = Math.Sin(_opalTilt * Math.PI / 180.0);
                return 2.0 * OpalD111 * Math.Sqrt(Math.Max(0.0, OpalNeff2 - s * s));
            }
        }
        /// <summary>Relative strength of the Bragg reflection (1 in air): weak-contrast scaling with (n_s - n_m)^2.</summary>
        public double OpalStrength
        {
            get { double dn = C.SilicaN - MediumN[_medium]; double dn0 = C.SilicaN - 1.0; return dn * dn / (dn0 * dn0); }
        }

        /// <summary>Detector count rate (cps) at glancing angle theta (deg).</summary>
        public double CountRate(double thetaDeg)
        {
            double sum = Background;
            double a = CrystalA[_crystal];
            double ka = _niFilter ? 0.5 : 1.0, kb = _niFilter ? 0.01 : C.CuKbRatio;
            for (int n = 0; n < 3; n++)
            {
                double d = a / (2.0 * (n + 1));
                sum += Peak(thetaDeg, C.CuKa1, d) * OrderHeight[n] * ka;
                sum += Peak(thetaDeg, C.CuKb, d) * OrderHeight[n] * kb;
            }
            return sum;
        }

        static double Peak(double thetaDeg, double lambda, double d)
        {
            double s = lambda / (2.0 * d);
            if (s >= 1.0)
                return 0.0;
            double th = Math.Asin(s) * 180.0 / Math.PI;
            double x = (thetaDeg - th) / PeakHwhm;
            return Math.Exp(-0.6931 * x * x);
        }

        public static double BraggTheta(double lambda, double d)
        {
            double s = lambda / (2.0 * d);
            return s >= 1.0 ? double.NaN : Math.Asin(s) * 180.0 / Math.PI;
        }

        /// <summary>Approximate visible-spectrum colour (Bruton's piecewise fit), black outside 380-750 nm.</summary>
        public static Color WavelengthColor(double nm)
        {
            float r = 0f, g = 0f, b = 0f;
            float w = (float)nm;
            if (w >= 380f && w < 440f) { r = -(w - 440f) / 60f; b = 1f; }
            else if (w >= 440f && w < 490f) { g = (w - 440f) / 50f; b = 1f; }
            else if (w >= 490f && w < 510f) { g = 1f; b = -(w - 510f) / 20f; }
            else if (w >= 510f && w < 580f) { r = (w - 510f) / 70f; g = 1f; }
            else if (w >= 580f && w < 645f) { r = 1f; g = -(w - 645f) / 65f; }
            else if (w >= 645f && w <= 750f) { r = 1f; }
            float f = 0f;
            if (w >= 380f && w < 420f) f = 0.3f + 0.7f * (w - 380f) / 40f;
            else if (w >= 420f && w <= 700f) f = 1f;
            else if (w > 700f && w <= 750f) f = 0.3f + 0.7f * (750f - w) / 50f;
            return new Color(r * f, g * f, b * f, 1f);
        }

        // ── Build ──

        protected override void BuildStation()
        {
            Material dark = CrystalFx.Solid(new Color(0.1f, 0.11f, 0.13f), 0.5f, 0.5f);
            Material steel = CrystalFx.Solid(new Color(0.55f, 0.58f, 0.62f), 0.85f, 0.55f);
            Material brass = CrystalFx.Solid(new Color(0.75f, 0.6f, 0.3f), 0.9f, 0.6f);

            // ---- Opal on a tilt stage ----
            var opal = new GameObject("Opal").transform;
            opal.SetParent(transform, false);
            opal.localPosition = OnTop(-0.48f, 0f, 0.02f);
            CrystalFx.Prim(PrimitiveType.Cube, "StageBase", opal, new Vector3(0f, 0.02f, 0f), new Vector3(0.2f, 0.04f, 0.16f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "Yoke", opal, new Vector3(0f, 0.07f, 0f), new Vector3(0.02f, 0.06f, 0.02f), steel, false);
            _opalSlab = new GameObject("TiltHead").transform;
            _opalSlab.SetParent(opal, false);
            _opalSlab.localPosition = new Vector3(0f, 0.11f, 0f);
            _opalMat = new Material(CrystalFx.Emissive(new Color(0.6f, 0.9f, 0.6f), 1f)) { name = "OpalPlay" };
            CrystalFx.Prim(PrimitiveType.Cube, "Matrix", _opalSlab, new Vector3(0f, -0.006f, 0f), new Vector3(0.13f, 0.012f, 0.1f), CrystalFx.Solid(new Color(0.25f, 0.22f, 0.2f), 0f, 0.3f), false);
            var face = CrystalFx.Prim(PrimitiveType.Cube, "OpalFace", _opalSlab, new Vector3(0f, 0.002f, 0f), new Vector3(0.12f, 0.004f, 0.09f), _opalMat, false);
            face.GetComponent<Renderer>().sharedMaterial = _opalMat;
            _opalTop = new GameObject("Top").transform;
            _opalTop.SetParent(_opalSlab, false);
            _opalTop.localPosition = new Vector3(0f, 0.005f, 0f);
            // White lamp straight above.
            CrystalFx.Prim(PrimitiveType.Cube, "LampPost", opal, new Vector3(0.11f, 0.22f, 0.06f), new Vector3(0.02f, 0.44f, 0.02f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "LampArm", opal, new Vector3(0.055f, 0.44f, 0.06f), new Vector3(0.12f, 0.02f, 0.02f), dark);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Lamp", opal, new Vector3(0f, 0.43f, 0f), new Vector3(0.06f, 0.025f, 0.06f), CrystalFx.Emissive(new Color(1f, 0.97f, 0.9f), 1.8f), false);
            _whiteBeam = Beam("WhiteBeam", opal, new Color(1f, 1f, 0.95f, 0.5f), 0.006f);
            _colourBeam = Beam("ColourBeam", opal, Color.white, 0.008f);
            var knob = CrystalFx.Prim(PrimitiveType.Cylinder, "TiltKnob", opal, new Vector3(-0.115f, 0.06f, -0.06f), new Vector3(0.045f, 0.012f, 0.045f), CrystalFx.Emissive(new Color(0.4f, 1f, 0.6f), 0.7f));
            knob.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            _tiltDial = knob.AddComponent<CrystalDial>();
            _tiltDial.Label = "Opal tilt";
            _tiltDial.TurnsForFull = 1f;
            _tiltDial.DesktopStep = 5f / 60f;
            _tiltDial.Changed = v => _opalTilt = v * 60.0;
            _opalText = CrystalFx.Text(opal, "OpalText", "", 0.11f, new Color(0.7f, 1f, 0.85f), new Vector3(0f, 0.3f, 0.1f), new Vector2(0.42f, 0.08f));

            // ---- X-ray goniometer ----
            var g = new GameObject("Goniometer").transform;
            g.SetParent(transform, false);
            g.localPosition = OnTop(0.32f, 0f, 0.0f);
            _goCentre = g;
            CrystalFx.Prim(PrimitiveType.Cylinder, "Base", g, new Vector3(0f, 0.01f, 0f), new Vector3(0.44f, 0.01f, 0.44f), dark);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Scale", g, new Vector3(0f, 0.021f, 0f), new Vector3(0.4f, 0.001f, 0.4f), brass, false);
            // Tube on the left, shooting along +x.
            _tube = new GameObject("Tube").transform;
            _tube.SetParent(g, false);
            _tube.localPosition = new Vector3(-0.2f, 0.06f, 0f);
            var tubeBody = CrystalFx.Prim(PrimitiveType.Cylinder, "TubeBody", _tube, new Vector3(-0.04f, 0f, 0f), new Vector3(0.06f, 0.06f, 0.06f), steel);
            tubeBody.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CrystalFx.Prim(PrimitiveType.Cube, "Hazard", _tube, new Vector3(-0.04f, 0.033f, 0f), new Vector3(0.05f, 0.004f, 0.03f), CrystalFx.Emissive(new Color(1f, 0.8f, 0f), 0.8f), false);
            CrystalFx.Prim(PrimitiveType.Cube, "Collimator", _tube, new Vector3(0.03f, 0f, 0f), new Vector3(0.04f, 0.012f, 0.012f), brass, false);
            // Sample turntable (theta) and detector arm (2 theta).
            _sample = new GameObject("Sample").transform;
            _sample.SetParent(g, false);
            _sample.localPosition = new Vector3(0f, 0.06f, 0f);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Post", g, new Vector3(0f, 0.035f, 0f), new Vector3(0.03f, 0.015f, 0.03f), steel, false);
            CrystalFx.Prim(PrimitiveType.Cube, "CrystalPlate", _sample, Vector3.zero, new Vector3(0.07f, 0.045f, 0.008f), CrystalFx.Crystal(new Color(0.92f, 0.95f, 1f, 0.6f), new Color(0.05f, 0.08f, 0.1f)), false);
            _arm = new GameObject("Arm").transform;
            _arm.SetParent(g, false);
            _arm.localPosition = new Vector3(0f, 0.03f, 0f);
            CrystalFx.Prim(PrimitiveType.Cube, "ArmBar", _arm, new Vector3(0.1f, 0f, 0f), new Vector3(0.2f, 0.01f, 0.025f), steel, false);
            _detector = new GameObject("Detector").transform;
            _detector.SetParent(_arm, false);
            _detector.localPosition = new Vector3(0.2f, 0.03f, 0f);
            var det = CrystalFx.Prim(PrimitiveType.Cylinder, "Geiger", _detector, new Vector3(0.03f, 0f, 0f), new Vector3(0.035f, 0.05f, 0.035f), CrystalFx.Solid(new Color(0.2f, 0.25f, 0.3f), 0.6f, 0.6f), false);
            det.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            _xIn = Beam("XrayIn", g, new Color(0.7f, 0.5f, 1f, 0.5f), 0.004f);
            _xOut = Beam("XrayOut", g, new Color(0.8f, 0.6f, 1f, 0.9f), 0.004f);
            var thKnob = CrystalFx.Prim(PrimitiveType.Cylinder, "ThetaKnob", g, new Vector3(-0.17f, 0.03f, -0.19f), new Vector3(0.05f, 0.012f, 0.05f), CrystalFx.Emissive(new Color(0.7f, 0.45f, 1f), 0.7f));
            thKnob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _thDial = thKnob.AddComponent<CrystalDial>();
            _thDial.Label = "Theta";
            _thDial.TurnsForFull = 3f;
            _thDial.DesktopStep = 0.5f / (ThMax - ThMin);
            _thDial.Value01 = (float)((_theta - ThMin) / (ThMax - ThMin));
            _thDial.Changed = v => { if (!_scanning) _theta = ThMin + v * (ThMax - ThMin); };

            // Rate meter + scan plot behind the goniometer.
            var scope = new GameObject("ScanPlot").transform;
            scope.SetParent(transform, false);
            scope.localPosition = OnTop(0.32f, 0.3f, 0.24f);
            CrystalFx.Prim(PrimitiveType.Cube, "Screen", scope, new Vector3(0f, 0f, 0.012f), new Vector3(0.56f, 0.3f, 0.02f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "Glass", scope, new Vector3(0f, 0f, -0.0005f), new Vector3(0.52f, 0.26f, 0.001f), CrystalFx.Line(new Color(0.02f, 0.03f, 0.05f, 1f)), false);
            CrystalFx.Prim(PrimitiveType.Cube, "Axis", scope, new Vector3(0f, -0.11f, -0.002f), new Vector3(0.5f, 0.0015f, 0.001f), CrystalFx.Line(new Color(0.35f, 0.3f, 0.5f, 1f)), false);
            CrystalFx.Text(scope, "AxisLbl", "2theta  10 ......................... 120 deg", 0.1f, new Color(0.75f, 0.65f, 1f), new Vector3(0f, -0.128f, -0.003f), new Vector2(0.5f, 0.03f));
            var pl = new GameObject("Plot");
            pl.transform.SetParent(scope, false);
            _plotLine = pl.AddComponent<LineRenderer>();
            _plotLine.useWorldSpace = false;
            _plotLine.widthMultiplier = 0.0025f;
            _plotLine.sharedMaterial = CrystalFx.Line(new Color(0.85f, 0.6f, 1f, 1f));
            _plotLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _plotLine.positionCount = ScanN;
            for (int i = 0; i < ScanN; i++)
                _scan[i] = -1f;
            _plotDirty = true;
            _bar = CrystalFx.Prim(PrimitiveType.Cube, "RateBar", scope, new Vector3(0.3f, -0.12f, -0.002f), new Vector3(0.02f, 0.001f, 0.004f), CrystalFx.Emissive(new Color(0.8f, 0.5f, 1f), 1.5f), false).transform;
            _gonioText = CrystalFx.Text(scope, "Rate", "", 0.11f, new Color(0.85f, 0.75f, 1f), new Vector3(0f, 0.17f, -0.003f), new Vector2(0.56f, 0.06f));
        }

        static LineRenderer Beam(string name, Transform parent, Color c, float w)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.widthMultiplier = w;
            lr.sharedMaterial = CrystalFx.Line(Color.white);
            lr.startColor = lr.endColor = c;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.deltaTime);

            // ---- Opal ----
            _opalSlab.localRotation = Quaternion.Euler(-(float)_opalTilt, 0f, 0f);
            double lamNm = OpalLambda * 1e9;
            double strength = OpalStrength;
            Color col = WavelengthColor(lamNm);
            bool visible = lamNm >= 380.0 && lamNm <= 750.0;
            float k = (float)Math.Min(1.0, strength) * (visible ? 1f : 0f);
            Color baseC = Color.Lerp(new Color(0.75f, 0.75f, 0.72f), col, 0.25f + 0.6f * k);
            baseC.a = 1f;
            if (_opalMat.HasProperty(BaseColorId)) _opalMat.SetColor(BaseColorId, baseC);
            if (_opalMat.HasProperty(EmissionId)) _opalMat.SetColor(EmissionId, col * (0.15f + 1.6f * k));
            Vector3 top = _opalTop.position;
            Vector3 lampPos = _opalSlab.parent.TransformPoint(new Vector3(0f, 0.415f, 0f));
            _whiteBeam.SetPosition(0, lampPos);
            _whiteBeam.SetPosition(1, top);
            // Reflection of the vertical beam off a face tilted by theta toward the player: up and back at 2 theta.
            float th = (float)_opalTilt * Mathf.Deg2Rad;
            Vector3 rLocal = new Vector3(0f, Mathf.Cos(2f * th), -Mathf.Sin(2f * th));
            Vector3 rWorld = _opalSlab.parent.TransformDirection(rLocal);
            _colourBeam.SetPosition(0, top);
            _colourBeam.SetPosition(1, top + rWorld * 0.35f);
            Color bc = col; bc.a = 0.15f + 0.8f * k;
            _colourBeam.startColor = _colourBeam.endColor = bc;
            if (visible && strength > 0.3 && lamNm >= 620.0)
                CrystalGoals.Latch("opal_red");
            if (visible && strength > 0.3 && lamNm <= 450.0)
                CrystalGoals.Latch("opal_blue");

            // ---- Goniometer ----
            if (_scanning)
            {
                _theta += dt * 2.0;
                if (_theta >= ThMax)
                {
                    _theta = ThMax;
                    _scanning = false;
                    CrystalGuideCard.SetChipLabel(_scanChip, "Scan 5-60");
                    QhysicsSfx.PlayAt(SfxId.CrystalRing, _goCentre.position, 0.3f, 1.4f);
                }
                _thDial.Value01 = (float)((_theta - ThMin) / (ThMax - ThMin));
            }
            _sample.localRotation = Quaternion.Euler(0f, -(float)_theta, 0f);
            _arm.localRotation = Quaternion.Euler(0f, -(float)(2.0 * _theta), 0f);
            double ideal = CountRate(_theta);
            // Counting statistics: a short running average of Poisson-ish noise (sqrt N over a 0.25 s gate).
            double noisy = Math.Max(0.0, ideal + (UnityEngine.Random.value - 0.5) * 2.0 * Math.Sqrt(ideal * 4.0) / 4.0);
            _cps += (noisy - _cps) * Math.Min(1.0, dt / 0.15);
            int idx = Mathf.Clamp(Mathf.RoundToInt((float)((_theta - ThMin) / 0.1)), 0, ScanN - 1);
            _scan[idx] = (float)_cps;
            _plotDirty = true;

            Vector3 centre = _sample.position;
            _xIn.SetPosition(0, _tube.TransformPoint(new Vector3(0.05f, 0f, 0f)));
            _xIn.SetPosition(1, centre);
            float rel = Mathf.Clamp01((float)((_cps - Background) / 300.0));
            _xOut.enabled = rel > 0.02f;
            if (_xOut.enabled)
            {
                _xOut.SetPosition(0, centre);
                _xOut.SetPosition(1, _detector.position);
                _xOut.startColor = _xOut.endColor = new Color(0.85f, 0.65f, 1f, 0.15f + 0.85f * rel);
            }
            float barH = Mathf.Clamp((float)Math.Log10(1.0 + _cps) / 3.2f, 0.005f, 1f) * 0.24f;
            _bar.localScale = new Vector3(0.02f, barH, 0.004f);
            _bar.localPosition = new Vector3(0.3f, -0.12f + barH * 0.5f, -0.002f);

            // Geiger clicks (capped at ~25 per second so it stays a crackle, not a tone).
            _clickAcc += dt * Mathf.Min(25f, (float)_cps * 0.05f);
            if (_clickAcc >= 1f)
            {
                _clickAcc -= 1f;
                if (UnityEngine.Random.value < 0.85f)
                    QhysicsSfx.PlayAt(SfxId.CrystalClick, _detector.position, 0.12f + 0.25f * rel, 1.8f + UnityEngine.Random.value * 0.4f);
            }

            // Goals: park (not scanning) on the NaCl (200) K-alpha peak, or on any K-beta peak with the filter off.
            if (!_scanning)
            {
                double dA = CrystalA[_crystal] / 2.0;
                if (_crystal == 0 && Peak(_theta, C.CuKa1, dA) > 0.7)
                    CrystalGoals.Latch("bragg_nacl");
                if (!_niFilter)
                {
                    for (int n = 0; n < 3; n++)
                        if (Peak(_theta, C.CuKb, CrystalA[_crystal] / (2.0 * (n + 1))) > 0.5 && Peak(_theta, C.CuKa1, CrystalA[_crystal] / (2.0 * (n + 1))) < 0.1)
                            CrystalGoals.Latch("bragg_kbeta");
                }
            }

            if (Time.unscaledTime >= _nextPlot && _plotDirty)
            {
                _nextPlot = Time.unscaledTime + 0.2f;
                _plotDirty = false;
                RedrawPlot();
            }

            if (Cell != null)
            {
                Cell.Drive = (float)(_theta / 90.0);
                Cell.Aux = rel;
                Cell.SetCaption(rel > 0.3 ? "in phase: waves from each plane add up\n-> strong reflection" : "out of phase: the planes' echoes cancel");
            }

            _tick += Time.deltaTime;
            if (_tick > 0.15f)
            {
                _tick = 0f;
                _opalText.text = "opal D " + (OpalD[_opalIdx] * 1e9).ToString("0") + " nm in " + MediumName[_medium] + "   tilt " + _opalTilt.ToString("0") + " deg\n"
                    + "<b>lambda " + lamNm.ToString("0") + " nm</b>" + (visible ? "" : (lamNm < 380 ? "  (UV - invisible)" : "  (IR - invisible)")) + "   strength " + (strength * 100).ToString("0") + "%";
                _gonioText.text = CrystalNames[_crystal] + " (100)   theta " + _theta.ToString("0.0") + "  2theta " + (2 * _theta).ToString("0.0") + " deg   <b>" + _cps.ToString("0") + " cps</b>"
                    + (_niFilter ? "   Ni filter" : "");
            }
        }

        void RedrawPlot()
        {
            // x: 2theta 10..120 deg over 0.5 m; y: log10(1 + cps) over 0.2 m.
            for (int i = 0; i < ScanN; i++)
            {
                float v = _scan[i] < 0f ? 0f : Mathf.Clamp01(Mathf.Log10(1f + _scan[i]) / 3.2f);
                float x = -0.25f + 0.5f * i / (ScanN - 1f);
                _plotLine.SetPosition(i, new Vector3(x, -0.11f + v * 0.2f, -0.003f));
            }
        }

        void ClearScan()
        {
            for (int i = 0; i < ScanN; i++)
                _scan[i] = -1f;
            _plotDirty = true;
        }

        void SetTheta(double th)
        {
            _scanning = false;
            CrystalGuideCard.SetChipLabel(_scanChip, "Scan 5-60");
            _theta = Math.Max(ThMin, Math.Min(ThMax, th));
            _thDial.Value01 = (float)((_theta - ThMin) / (ThMax - ThMin));
        }

        protected override CrystalGuideCard.Content GetContent()
        {
            double thNaCl = BraggTheta(C.CuKa1, C.NaClA / 2.0);
            return new CrystalGuideCard.Content
            {
                Title = "9  BRAGG DIFFRACTION  - opal colours and X-ray crystallography",
                ChallengeId = ChallengeId,
                What =
                    "A crystal is a stack of evenly spaced layers. Each layer reflects a tiny bit of a wave. Usually the echoes cancel, but at one special angle the extra path between neighbouring layers is exactly one wavelength and they all add up: a bright reflection.\n\n"
                    + "Opal is a stack of glass beads about 250 nm across - the right spacing for visible light. Tilt it and the colour slides from green to blue. Soak it in water and the colour almost disappears, because water and glass bend light nearly the same.\n\n"
                    + "Atoms in salt are ~0.28 nm apart - the right spacing for X-rays. Turn the salt crystal and the counter jumps at sharp angles. Measuring those angles is how we learned where atoms sit (the Braggs, 1913).\n\n"
                    + "<color=#9FB4C4>Model: exact Bragg angles; peak heights and widths are hand-set.</color>",
                Math =
                    "<b>Bragg</b>   n lambda = 2 d sin(theta)   (theta = glancing angle, detector at 2 theta)\n"
                    + "<b>Cubic (100) face</b>   d(h00) = a / h  ->  (200), (400), (600)\n"
                    + "<b>NaCl + Cu K-alpha</b>   sin theta = 154.06 / 564.0  ->  theta = " + thNaCl.ToString("0.00") + " deg\n"
                    + "<b>Opal (Bragg-Snell)</b>   lambda = 2 d111 sqrt(n_eff^2 - sin^2 theta),  d111 = sqrt(2/3) D\n"
                    + "<b>Effective index</b>   n_eff^2 = 0.74 n_silica^2 + 0.26 n_medium^2\n"
                    + "<b>Brightness</b>   ~ (n_silica - n_medium)^2   (weak-contrast scaling)\n\n"
                    + "a(NaCl) = 564.0 pm, a(LiF) = 402.7 pm, a(KCl) = 629.3 pm; Cu K-alpha1 154.06 pm, K-beta 139.22 pm; silica n = 1.45  [W.H. & W.L. Bragg, Proc. R. Soc. A 88, 428 (1913); Intl. Tables for Crystallography Vol. C; Sanders, Nature 204, 1151 (1964)]",
                TryThis =
                    "1. Opal: twist the green knob (or Tilt chips). The reflected beam swings and its colour shifts toward blue.\n"
                    + "2. Swap to the 300 nm opal: now it starts red. Try water: the play of colour fades.\n"
                    + "3. X-rays: press 'Scan 5-60' and watch the purple plot. Each sharp spike is a set of planes.\n"
                    + "4. Find the big NaCl peak near theta = " + thNaCl.ToString("0.0") + " deg with the purple knob or theta chips, and stop on it.\n"
                    + "5. Every K-alpha peak has a smaller K-beta twin at a lower angle. Ni filter ON eats K-beta.\n"
                    + "6. Switch to KCl or LiF: the spikes move. From theta, a = lambda / sin(theta) for (200).",
                Lattice =
                    "The model shows three planes of ions (red Na+, blue Cl-) and the incoming ray (model).\n\n"
                    + "The ray reflected by the second plane travels an extra 2 d sin(theta). When that equals a whole number of wavelengths, all the reflections line up crest-to-crest and add.\n\n"
                    + "In NaCl the Na+ and Cl- sit on the same (200) planes, so those reflect strongly. In an opal the 'atoms' are silica spheres, 1000 times bigger, so the same rule picks out visible light instead of X-rays."
            };
        }

        protected override void AddControls(CrystalGuideCard card)
        {
            card.AddControl("Tilt +5", () => _tiltDial.Set(_tiltDial.Value01 + 5f / 60f));
            card.AddControl("Tilt -5", () => _tiltDial.Set(_tiltDial.Value01 - 5f / 60f));
            _opalChip = card.AddControl("Opal: 250 nm", () =>
            {
                _opalIdx = (_opalIdx + 1) % OpalD.Length;
                CrystalGuideCard.SetChipLabel(_opalChip, "Opal: " + (OpalD[_opalIdx] * 1e9).ToString("0") + " nm");
            });
            _mediumChip = card.AddControl("Pores: air", () =>
            {
                _medium = (_medium + 1) % MediumN.Length;
                CrystalGuideCard.SetChipLabel(_mediumChip, "Pores: " + MediumName[_medium]);
                QhysicsSfx.PlayAt(SfxId.GasWhoosh, _opalSlab.position, 0.2f, 1.6f);
            });
            _scanChip = card.AddControl("Scan 5-60", () =>
            {
                if (_scanning) { _scanning = false; CrystalGuideCard.SetChipLabel(_scanChip, "Scan 5-60"); return; }
                ClearScan();
                _theta = ThMin;
                _scanning = true;
                CrystalGuideCard.SetChipLabel(_scanChip, "Stop scan");
            });
            card.AddControl("Clear plot", ClearScan);
            card.AddControl("th +1", () => SetTheta(_theta + 1.0));
            card.AddControl("th -1", () => SetTheta(_theta - 1.0));
            card.AddControl("th +0.1", () => SetTheta(_theta + 0.1));
            card.AddControl("th -0.1", () => SetTheta(_theta - 0.1));
            _crystalChip = card.AddControl("Crystal: NaCl", () =>
            {
                _crystal = (_crystal + 1) % CrystalA.Length;
                ClearScan();
                CrystalGuideCard.SetChipLabel(_crystalChip, "Crystal: " + CrystalNames[_crystal]);
            });
            _filterChip = card.AddControl("Ni filter: OFF", () =>
            {
                _niFilter = !_niFilter;
                CrystalGuideCard.SetChipLabel(_filterChip, _niFilter ? "Ni filter: ON" : "Ni filter: OFF");
            });
        }

        protected override string LiveLine()
        {
            double sinT = Math.Sin(_theta * Math.PI / 180.0);
            double aIf200 = C.CuKa1 / Math.Max(1e-6, sinT);
            return "opal: lambda = 2 x " + (OpalD111 * 1e9).ToString("0") + " nm x sqrt(" + OpalNeff2.ToString("0.000") + " - sin^2 " + _opalTilt.ToString("0") + ") = "
                + (OpalLambda * 1e9).ToString("0") + " nm\n"
                + "X-ray: theta " + _theta.ToString("0.00") + " deg  " + _cps.ToString("0") + " cps   if (200) K-alpha: a = lambda / sin theta = " + (aIf200 * 1e12).ToString("0") + " pm";
        }
    }
}
