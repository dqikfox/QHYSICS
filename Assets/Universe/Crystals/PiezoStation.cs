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
    /// <summary>
    /// A piezoelectric element with electrodes. Direct effect: a change of force dF puts dQ = d * dF on the
    /// electrodes; the voltage is V = Q / C with C = eps0 * epsR * A / t + C_load. Charge leaks away through
    /// R_leak (tau = R C), which is why piezo sensors read changes, not steady forces. A strike is treated as a
    /// short force pulse F = m * v / tau_contact (contact time assumption 0.5 ms for a steel hammer). If |V|
    /// reaches the spark gap's breakdown (3 MV/m * gap) the gap sparks and the charge is dumped.
    /// Honesty: lumped single-axis model (one d coefficient, uniform stress, ideal electrodes, no depoling or
    /// cracking); breakdown uses the 3 kV/mm rule of thumb, not a full Paschen curve.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PiezoCrystal : MonoBehaviour, ICrystalInspectable, IQhysicsInteractable
    {
        public const double ContactTime = 5e-4;
        public bool Pzt;
        public double LoadCap = 2e-12;   // meter + leads
        public double LeakOhms = 1e13;   // insulation / electrometer input (tau ~ 30 s for quartz)
        public double GapM = 1e-3;       // spark gap
        public double StaticForce;       // N, from the press or a finger squeeze
        public bool IsStationPress;
        public Transform GapA, GapB;     // spark endpoints (null = electrodes)
        public Action<double> Sparked;
        public Action<double> Struck;

        double _lastForce;
        double _charge;
        double _peakV;
        float _peakUntil;
        float _lastStrikeAt = -10f;
        float _lastSpark = -10f;
        public double LastStrikeForce { get; private set; }
        public int SparkCount { get; private set; }

        public string MaterialName => Pzt ? "PZT-5A element" : "alpha-quartz X-cut";
        public double D => Pzt ? C.PztD33 : C.QuartzD11;
        public double EpsR => Pzt ? C.PztEpsR : C.QuartzEpsR;
        /// <summary>Element geometry: quartz 10 x 10 x 5 mm block; PZT 2.6 mm dia x 6 mm igniter cylinder.</summary>
        public double Area => Pzt ? Math.PI * 1.3e-3 * 1.3e-3 : 1e-4;
        public double Thick => Pzt ? 6e-3 : 5e-3;
        public double CrystalCap => C.Eps0 * EpsR * Area / Thick;
        public double Capacitance => CrystalCap + LoadCap;
        public double Charge => _charge;
        public double Voltage => _charge / Capacitance;
        public double PeakVoltage => Time.unscaledTime < _peakUntil ? _peakV : Voltage;
        public double BreakdownV => C.AirBreakdown * GapM;
        public double Tau => LeakOhms * Capacitance;
        /// <summary>Thickness-mode resonance: quartz N = v/2 = 2860 Hz*m, PZT-5A N33 ~1500 Hz*m.</summary>
        public double ResonanceHz => (Pzt ? 1500.0 : C.QuartzVx * 0.5) / Thick;
        public double MechQ => Pzt ? 75.0 : 1e4;

        public void ResetCharge()
        {
            _charge = 0;
            _lastForce = StaticForce;
            _peakV = 0;
        }

        void Update()
        {
            double dt = Math.Max(0.0, Time.deltaTime);
            double dF = StaticForce - _lastForce;
            _lastForce = StaticForce;
            _charge += D * dF;
            if (Tau > 1e-9 && dt > 0)
                _charge *= Math.Exp(-dt / Tau);
            double v = Voltage;
            if (Math.Abs(v) > Math.Abs(_peakV) || Time.unscaledTime >= _peakUntil)
            {
                _peakV = v;
                _peakUntil = Time.unscaledTime + 1.5f;
            }
            if (Math.Abs(v) >= BreakdownV)
                DoSpark(v);
            if (IsStationPress && !Pzt && StaticForce > 1.0)
                CrystalGoals.Report("piezo_press_v", (float)Math.Abs(v));
        }

        /// <summary>Hammer / impact: short force pulse of peak <paramref name="force"/> newtons.</summary>
        public void Strike(double force, Vector3 at)
        {
            if (force < 1.0 || Time.unscaledTime - _lastStrikeAt < 0.06f)
                return;
            _lastStrikeAt = Time.unscaledTime;
            LastStrikeForce = force;
            double vPeak = Voltage + D * force / Capacitance;
            _peakV = vPeak;
            _peakUntil = Time.unscaledTime + 2f;
            QhysicsSfx.PlayAt(SfxId.CrystalClick, at, Mathf.Clamp01(0.3f + (float)(force / 4000.0)), Pzt ? 0.8f : 1.1f);
            if (Struck != null)
                Struck(vPeak);
            if (Math.Abs(vPeak) >= BreakdownV)
                DoSpark(vPeak);
            // The pulse is symmetric (force on then off): net charge returns unless a spark dumped it mid-pulse.
        }

        void DoSpark(double v)
        {
            if (Time.unscaledTime - _lastSpark < 0.05f)
                return;
            _lastSpark = Time.unscaledTime;
            SparkCount++;
            Vector3 a = GapA != null ? GapA.position : transform.position + transform.up * 0.012f;
            Vector3 b = GapB != null ? GapB.position : transform.position - transform.up * 0.012f;
            CrystalFx.Spark(a, b, new Color(0.7f, 0.85f, 1f));
            QhysicsSfx.PlayAt(SfxId.SparkSnap, (a + b) * 0.5f, 0.75f);
            _charge = 0;
            _lastForce = StaticForce;
            if (!Pzt)
                CrystalGoals.Latch("piezo_quartz_spark");
            if (Sparked != null)
                Sparked(v);
        }

        // ── Inspect ──
        public string InspectStats(bool held)
        {
            return MaterialName + " | V " + CrystalFx.FormatVolts(Voltage) + " | Q " + CrystalFx.FormatCharge(_charge) + " | " + (held ? "held" : "hover");
        }

        public string InspectDetail()
        {
            return "d " + (D * 1e12).ToString("0.#") + " pC/N | C " + CrystalFx.FormatCap(Capacitance) + " | tau " + Tau.ToString("0.0") + " s | gap " + CrystalFx.FormatVolts(BreakdownV);
        }

        public string InspectHint(bool held)
        {
            return held ? "Trigger = squeeze (+), release = (-)" : "Strike it hard with the hammer";
        }

        // ── Desktop E: a firm finger squeeze-and-release tap ──
        public string InteractLabel => "Tap";
        public bool CanInteract(GameObject actor) => true;
        public void Interact(GameObject actor) => Strike(60.0, transform.position);
        public void SetHover(bool hovered) { }
    }

    /// <summary>Steel hammer: converts the normal impact speed on a PiezoCrystal into a force pulse F = m v / tau_c.</summary>
    [DisallowMultipleComponent]
    public sealed class CrystalHammer : MonoBehaviour, IDesktopActivatable
    {
        Rigidbody _rb;
        XRGrabInteractable _grab;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _grab = GetComponent<XRGrabInteractable>();
        }

        void OnCollisionEnter(Collision c)
        {
            if (c == null || c.collider == null || c.contactCount == 0)
                return;
            var piezo = c.collider.GetComponentInParent<PiezoCrystal>();
            float vn = Mathf.Abs(Vector3.Dot(c.relativeVelocity, c.GetContact(0).normal));
            if (piezo != null && vn > 0.25f)
            {
                double f = (_rb != null ? _rb.mass : 0.45) * vn / PiezoCrystal.ContactTime;
                piezo.Strike(f, c.GetContact(0).point);
                CrystalFx.PulseHolders(_grab, Mathf.Clamp01(0.25f + vn * 0.12f), 0.05f);
            }
        }

        /// <summary>Desktop LMB while held: a 4 m/s swing at the nearest crystal within 0.6 m.</summary>
        public void DesktopActivate(int delta)
        {
            PiezoCrystal best = null;
            float bd = 0.36f;
            foreach (var p in UnityEngine.Object.FindObjectsByType<PiezoCrystal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            if (best != null)
                best.Strike((_rb != null ? _rb.mass : 0.45) * (delta < 0 ? 2.0 : 4.5) / PiezoCrystal.ContactTime, best.transform.position);
            else
                QhysicsSfx.Play2D(SfxId.Whoosh, 0.4f);
        }
    }

    /// <summary>Lab gas burner: gas valve + spark gap at the nozzle; flame is a heat source for the crystals.</summary>
    [DisallowMultipleComponent]
    public sealed class CrystalBurner : MonoBehaviour
    {
        public static readonly List<CrystalBurner> All = new List<CrystalBurner>();
        public Transform Nozzle;
        public bool Gas { get; private set; }
        public bool Lit { get; private set; }
        public Action<bool> Changed;
        GameObject _flame;
        Light _light;
        CrystalHeatSource _heat;
        float _gasSince;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void Build(Transform nozzle, GameObject flame, Light light)
        {
            Nozzle = nozzle;
            _flame = flame;
            _light = light;
            _heat = nozzle.gameObject.AddComponent<CrystalHeatSource>();
            _heat.TempC = 700f;
            _heat.Radius = 0.14f;
            _heat.On = false;
            Apply();
        }

        public void SetGas(bool on)
        {
            Gas = on;
            if (on) _gasSince = Time.time;
            if (!on) Lit = false;
            Apply();
        }

        /// <summary>A spark at the nozzle. Returns true when it lit the flame.</summary>
        public bool TryIgnite(string byWhat)
        {
            if (!Gas || Lit)
                return false;
            Lit = true;
            Apply();
            QhysicsSfx.PlayAt(SfxId.GasWhoosh, Nozzle.position, 0.7f);
            Debug.Log("CrystalLab: burner lit by " + byWhat + ".");
            return true;
        }

        void Apply()
        {
            if (_flame != null) _flame.SetActive(Lit);
            if (_light != null) _light.enabled = Lit;
            if (_heat != null) _heat.On = Lit;
            if (Changed != null) Changed(Lit);
        }

        void Update()
        {
            if (!Lit || _flame == null)
                return;
            float f = 1f + 0.08f * Mathf.Sin(Time.time * 23f) + 0.05f * Mathf.Sin(Time.time * 37f);
            _flame.transform.localScale = new Vector3(0.035f, 0.07f * f, 0.035f);
            if (_light != null)
                _light.intensity = 0.9f * f;
        }

        public static CrystalBurner Near(Vector3 p, float radius)
        {
            CrystalBurner best = null;
            float bd = radius * radius;
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] == null || All[i].Nozzle == null)
                    continue;
                float d = (All[i].Nozzle.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = All[i]; }
            }
            return best;
        }
    }

    /// <summary>
    /// Click igniter (toolbelt CRYSTAL > Igniter): pull the trigger and a spring hammer (3 g at ~4 m/s, contact
    /// ~20 us) hits a PZT-5A cylinder: F ~ 600 N, V = d33 F / C ~ 15 kV, enough to jump a few-mm gap at the tip.
    /// Lights a CrystalBurner whose nozzle is within 8 cm while its gas is on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrystalIgniter : MonoBehaviour, IDesktopActivatable, ICrystalInspectable
    {
        public const string RootName = "Gadget_PiezoIgniter";
        XRGrabInteractable _grab;
        Transform _tipA, _tipB;
        double _lastV;
        int _clicks;

        public static GameObject Spawn(Vector3 pos)
        {
            var root = new GameObject(RootName);
            root.transform.position = pos;
            Material body = CrystalFx.Solid(new Color(0.85f, 0.35f, 0.12f), 0.1f, 0.5f);
            Material steel = CrystalFx.Solid(new Color(0.7f, 0.72f, 0.75f), 0.8f, 0.6f);
            CrystalFx.Prim(PrimitiveType.Cube, "Grip", root.transform, new Vector3(0f, -0.04f, -0.05f), new Vector3(0.03f, 0.09f, 0.04f), body);
            CrystalFx.Prim(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.035f, 0.035f, 0.13f), body);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Wand", root.transform, new Vector3(0f, 0.01f, 0.15f), new Vector3(0.012f, 0.09f, 0.012f), steel, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var a = new GameObject("TipA").transform; a.SetParent(root.transform, false); a.localPosition = new Vector3(0f, 0.016f, 0.245f);
            var b = new GameObject("TipB").transform; b.SetParent(root.transform, false); b.localPosition = new Vector3(0f, 0.004f, 0.245f);
            var ig = root.AddComponent<CrystalIgniter>();
            ig._tipA = a; ig._tipB = b;
            CrystalFx.MakeGrabbable(root, 0.12f);
            ig._grab = root.GetComponent<XRGrabInteractable>();
            ig._grab.activated.AddListener(_ => ig.Click());
            return root;
        }

        public void Click()
        {
            _clicks++;
            double m = 0.003, v = 4.0, tc = 20e-6;
            double f = m * v / tc;
            double cap = C.Eps0 * C.PztEpsR * Math.PI * 1.3e-3 * 1.3e-3 / 6e-3 + 2e-12;
            _lastV = C.PztD33 * f / cap;
            QhysicsSfx.PlayAt(SfxId.CrystalClick, transform.position, 0.6f, 0.7f);
            QhysicsSfx.PlayAt(SfxId.SparkSnap, _tipA.position, 0.7f);
            CrystalFx.Spark(_tipA.position, _tipB.position, new Color(0.65f, 0.8f, 1f));
            CrystalFx.PulseHolders(_grab, 0.45f, 0.03f);
            CrystalBurner burner = CrystalBurner.Near(_tipA.position, 0.08f);
            if (burner != null && burner.TryIgnite("piezo igniter"))
                CrystalGoals.Latch("burner_lit_piezo");
        }

        public void DesktopActivate(int delta) => Click();

        public string InspectStats(bool held) => "Piezo igniter | last spark " + CrystalFx.FormatVolts(_lastV) + " | clicks " + _clicks;
        public string InspectDetail() => "F = m v / t_c = 0.003 kg x 4 m/s / 20 us = 600 N; V = d33 F / C";
        public string InspectHint(bool held) => held ? "Trigger: click | hold tip at a burner with gas on" : "Grab | pull trigger to spark";
    }

    /// <summary>Station 1: squeeze or strike quartz, light a burner from the spark, and run the effect backwards.</summary>
    public sealed class PiezoStation : CrystalStation
    {
        public override string Id => "piezo";
        public override string Title => "Piezoelectricity";
        public override string ChallengeId => "crystal_piezo_burner";
        protected override CrystalUnitCell.Mode CellMode => CrystalUnitCell.Mode.QuartzPiezo;
        protected override string CellTitle => "quartz: Si(+) / O(-) ring";

        const double PressMaxN = 2000.0;
        PiezoCrystal _piezo;
        CrystalDial _dial;
        CrystalBurner _burner;
        Transform _platen, _screw, _crystalVis, _wheel;
        Renderer _crystalR;
        TextMeshPro _meter;
        Transform _bar;
        Material _quartzMat, _pztMat;
        CrystalHum _hum;
        int _driveMode; // 0 off, 1 = 100 V DC, 2 = 100 V @ 1 kHz, 3 = resonance
        UnityEngine.UI.Button _gasChip, _matChip, _driveChip;
        float _sparkFlashUntil;

        protected override void BuildStation()
        {
            Material steel = CrystalFx.Solid(new Color(0.55f, 0.58f, 0.62f), 0.85f, 0.55f);
            Material dark = CrystalFx.Solid(new Color(0.12f, 0.13f, 0.15f), 0.6f, 0.5f);
            Material gold = CrystalFx.Solid(new Color(0.95f, 0.75f, 0.3f), 1f, 0.75f);
            _quartzMat = CrystalFx.Crystal(new Color(0.85f, 0.93f, 1f, 0.5f), new Color(0.05f, 0.12f, 0.16f));
            _pztMat = CrystalFx.Solid(new Color(0.42f, 0.42f, 0.44f), 0.1f, 0.3f);

            // ── Screw press (left) ──
            var press = new GameObject("Press").transform;
            press.SetParent(transform, false);
            press.localPosition = OnTop(-0.45f, 0f, 0.02f);
            CrystalFx.Prim(PrimitiveType.Cube, "Base", press, new Vector3(0f, 0.015f, 0f), new Vector3(0.32f, 0.03f, 0.22f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "PillarL", press, new Vector3(-0.13f, 0.23f, 0f), new Vector3(0.03f, 0.42f, 0.03f), steel);
            CrystalFx.Prim(PrimitiveType.Cube, "PillarR", press, new Vector3(0.13f, 0.23f, 0f), new Vector3(0.03f, 0.42f, 0.03f), steel);
            CrystalFx.Prim(PrimitiveType.Cube, "Beam", press, new Vector3(0f, 0.45f, 0f), new Vector3(0.32f, 0.04f, 0.06f), dark);
            CrystalFx.Prim(PrimitiveType.Cube, "Anvil", press, new Vector3(0f, 0.05f, 0f), new Vector3(0.09f, 0.04f, 0.09f), steel);
            CrystalFx.Prim(PrimitiveType.Cube, "ElectrodeLo", press, new Vector3(0f, 0.072f, 0f), new Vector3(0.05f, 0.004f, 0.05f), gold, false);
            var crystal = CrystalFx.Prim(PrimitiveType.Cube, "QuartzElement", press, new Vector3(0f, 0.084f, 0f), new Vector3(0.04f, 0.02f, 0.04f), _quartzMat);
            _crystalVis = crystal.transform;
            _crystalR = crystal.GetComponent<Renderer>();
            CrystalFx.Prim(PrimitiveType.Cube, "ElectrodeHi", crystal.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1.2f, 0.1f, 1.2f), gold, false);
            _piezo = crystal.AddComponent<PiezoCrystal>();
            _piezo.IsStationPress = true;
            crystal.AddComponent<XRSimpleInteractable>(); // hover = INSPECT strip
            var platen = CrystalFx.Prim(PrimitiveType.Cube, "Platen", press, new Vector3(0f, 0.22f, 0f), new Vector3(0.1f, 0.03f, 0.1f), steel);
            _platen = platen.transform;
            var screw = CrystalFx.Prim(PrimitiveType.Cylinder, "Screw", press, new Vector3(0f, 0.33f, 0f), new Vector3(0.018f, 0.12f, 0.018f), steel, false);
            _screw = screw.transform;
            var wheel = CrystalFx.Prim(PrimitiveType.Cylinder, "Handwheel", press, new Vector3(0f, 0.5f, 0f), new Vector3(0.15f, 0.012f, 0.15f), CrystalFx.Emissive(new Color(0f, 0.75f, 0.9f), 0.6f));
            _wheel = wheel.transform;
            CrystalFx.FlatCollider(wheel);
            for (int k = 0; k < 3; k++)
            {
                var knob = CrystalFx.Prim(PrimitiveType.Cylinder, "Spoke" + k, _wheel, Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(0.45f, 2.5f, 0f), new Vector3(0.12f, 2.5f, 0.12f), dark, false);
                knob.transform.localRotation = Quaternion.identity;
            }
            _dial = wheel.AddComponent<CrystalDial>();
            _dial.Label = "Press";
            _dial.TurnsForFull = 1.5f;
            _dial.DesktopStep = 0.1f;
            CrystalFx.Text(press, "PressLabel", "TWIST WHEEL\nclockwise = squeeze", 0.16f, new Color(0.6f, 0.9f, 1f, 0.9f), new Vector3(0f, 0.56f, -0.02f), new Vector2(0.4f, 0.1f));

            // ── Meter (middle) ──
            var meter = CrystalFx.Prim(PrimitiveType.Cube, "Electrometer", transform, OnTop(0.05f, 0.12f, 0.17f), new Vector3(0.34f, 0.24f, 0.05f), dark);
            _meter = CrystalFx.Text(transform, "MeterText", "", 0.19f, new Color(0.55f, 1f, 0.85f), OnTop(0.05f, 0.14f, 0.143f), new Vector2(0.32f, 0.18f), TextAlignmentOptions.TopLeft);
            var bar = CrystalFx.Prim(PrimitiveType.Cube, "Bar", transform, OnTop(0.05f, 0.025f, 0.142f), new Vector3(0.001f, 0.012f, 0.004f), CrystalFx.Emissive(new Color(0f, 0.9f, 1f), 1.4f), false);
            _bar = bar.transform;

            // ── Burner with spark gap (right) ──
            var burnerRoot = new GameObject("Burner").transform;
            burnerRoot.SetParent(transform, false);
            burnerRoot.localPosition = OnTop(0.55f, 0f, 0.05f);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Foot", burnerRoot, new Vector3(0f, 0.012f, 0f), new Vector3(0.12f, 0.012f, 0.12f), dark);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Tube", burnerRoot, new Vector3(0f, 0.11f, 0f), new Vector3(0.028f, 0.1f, 0.028f), steel);
            var nozzle = new GameObject("Nozzle").transform; nozzle.SetParent(burnerRoot, false); nozzle.localPosition = new Vector3(0f, 0.215f, 0f);
            var gA = CrystalFx.Prim(PrimitiveType.Cylinder, "GapElectrodeA", burnerRoot, new Vector3(-0.02f, 0.225f, 0f), new Vector3(0.004f, 0.02f, 0.004f), gold, false);
            gA.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var gB = CrystalFx.Prim(PrimitiveType.Cylinder, "GapElectrodeB", burnerRoot, new Vector3(0.022f, 0.225f, 0f), new Vector3(0.004f, 0.02f, 0.004f), gold, false);
            gB.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var tipA = new GameObject("GapTipA").transform; tipA.SetParent(burnerRoot, false); tipA.localPosition = new Vector3(-0.0005f, 0.225f, 0f);
            var tipB = new GameObject("GapTipB").transform; tipB.SetParent(burnerRoot, false); tipB.localPosition = new Vector3(0.0035f, 0.225f, 0f);
            _piezo.GapA = tipA; _piezo.GapB = tipB;
            var flame = CrystalFx.Prim(PrimitiveType.Capsule, "Flame", burnerRoot, new Vector3(0f, 0.28f, 0f), new Vector3(0.035f, 0.07f, 0.035f),
                CrystalFx.Crystal(new Color(0.35f, 0.55f, 1f, 0.55f), new Color(0.3f, 0.5f, 1.4f)), false);
            var fl = new GameObject("FlameLight").AddComponent<Light>();
            fl.transform.SetParent(burnerRoot, false);
            fl.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            fl.type = LightType.Point; fl.range = 1.2f; fl.color = new Color(0.55f, 0.65f, 1f); fl.shadows = LightShadows.None;
            _burner = burnerRoot.gameObject.AddComponent<CrystalBurner>();
            _burner.Build(nozzle, flame, fl);
            var valve = CrystalFx.Prim(PrimitiveType.Cylinder, "GasValve", burnerRoot, new Vector3(0f, 0.05f, -0.05f), new Vector3(0.035f, 0.012f, 0.035f), CrystalFx.Solid(new Color(0.9f, 0.2f, 0.15f)));
            valve.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var vt = valve.AddComponent<CrystalToggle>();
            vt.Label = "Gas";
            vt.Toggled = ToggleGas;
            CrystalFx.Text(burnerRoot, "GapLabel", "spark gap 1 mm (shown 4 mm)\nbreakdown ~3 kV", 0.13f, new Color(0.6f, 0.9f, 1f, 0.9f), new Vector3(0f, 0.37f, 0f), new Vector2(0.4f, 0.08f));

            // Wires: crystal electrodes -> meter -> gap (unlit lines).
            Wire(press.TransformPoint(new Vector3(0f, 0.095f, 0.02f)), transform.TransformPoint(OnTop(-0.12f, 0.06f, 0.17f)), transform.TransformPoint(OnTop(0.53f, 0.225f, 0.05f)));
            Wire(press.TransformPoint(new Vector3(0f, 0.072f, 0.03f)), transform.TransformPoint(OnTop(-0.12f, 0.03f, 0.18f)), transform.TransformPoint(OnTop(0.575f, 0.225f, 0.05f)));

            // ── Hammer (front) ──
            var hammer = CrystalGadgets.CreateHammer("Crystal_Hammer", transform.TransformPoint(OnTop(-0.05f, 0.025f, -0.2f)), transform.rotation * Quaternion.Euler(0f, 90f, 90f));
            CrystalHomeReturn.Attach(hammer, transform);

            _hum = CrystalHum.Attach(crystal);
            _dial.Changed = OnDial;
            _piezo.Sparked = OnSpark;
            ApplyPlaten();
        }

        void Wire(Vector3 a, Vector3 mid, Vector3 b)
        {
            var g = new GameObject("Wire");
            g.transform.SetParent(transform, true);
            var lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 3;
            lr.SetPosition(0, a); lr.SetPosition(1, mid); lr.SetPosition(2, b);
            lr.widthMultiplier = 0.003f;
            lr.sharedMaterial = CrystalFx.Line(new Color(0.85f, 0.55f, 0.2f, 1f));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnDial(float v) => ApplyPlaten();

        double PressForce()
        {
            float v = _dial != null ? _dial.Value01 : 0f;
            return Math.Max(0.0, (v - 0.25) / 0.75) * PressMaxN;
        }

        void ApplyPlaten()
        {
            float v = _dial != null ? _dial.Value01 : 0f;
            float contact = Mathf.Clamp01(v / 0.25f);
            if (_platen != null)
                _platen.localPosition = new Vector3(0f, Mathf.Lerp(0.22f, 0.11f, contact), 0f);
            if (_screw != null)
                _screw.localPosition = new Vector3(0f, Mathf.Lerp(0.33f, 0.275f, contact), 0f);
            if (_wheel != null)
                _wheel.localRotation = Quaternion.Euler(0f, v * 540f, 0f);
            if (_piezo != null)
                _piezo.StaticForce = PressForce();
        }

        void ToggleGas()
        {
            _burner.SetGas(!_burner.Gas);
            CrystalGuideCard.SetChipLabel(_gasChip, _burner.Gas ? "Gas: ON" : "Gas: OFF");
        }

        void OnSpark(double v)
        {
            _sparkFlashUntil = Time.unscaledTime + 0.4f;
            if (_burner.TryIgnite("piezo spark (" + _piezo.MaterialName + ")"))
                CrystalGoals.Latch("burner_lit_piezo");
        }

        void Update()
        {
            if (_piezo == null)
                return;
            double v = _piezo.Voltage;
            // Converse effect: strain = d * E -> dL = d * V (thickness mode); x Q near resonance.
            double driveV = 100.0;
            double dl = 0.0;
            float visual = 0f;
            float hz = 0f;
            switch (_driveMode)
            {
                case 1: dl = _piezo.D * driveV; visual = 0.25f; break;
                case 2: dl = _piezo.D * driveV * Math.Sin(Time.time * 2.0 * Math.PI * 6.0); visual = 0.25f * Mathf.Sin(Time.time * Mathf.PI * 12f); hz = 1000f; break;
                case 3: dl = _piezo.D * driveV * _piezo.MechQ * Math.Sin(Time.time * 2.0 * Math.PI * 9.0); visual = 0.7f * Mathf.Sin(Time.time * Mathf.PI * 18f); hz = (float)Audible(_piezo.ResonanceHz); break;
            }
            if (_crystalVis != null)
            {
                float sq = (float)(_piezo.StaticForce / PressMaxN) * 0.12f;
                _crystalVis.localScale = new Vector3(0.04f * (1f + sq * 0.3f), 0.02f * (1f - sq + visual * 0.15f), 0.04f * (1f + sq * 0.3f));
            }
            if (_hum != null)
                _hum.SetTone(hz, _driveMode >= 2 ? (_driveMode == 3 ? 0.35f : 0.12f) : 0f);

            if (Cell != null)
            {
                float stress = Mathf.Clamp((float)(v / Math.Max(1.0, _piezo.BreakdownV)), -1f, 1f);
                if (_driveMode > 0) stress = Mathf.Clamp(visual * 1.4f, -1f, 1f);
                Cell.Drive = stress;
                Cell.SetCaption(_driveMode > 0 ? "applied field pushes ions:\nthe crystal changes shape" : "squeeze shifts + vs - centres\n-> dipole -> surface charge");
            }

            if (_meter != null)
            {
                double peak = _piezo.PeakVoltage;
                bool spark = Time.unscaledTime < _sparkFlashUntil;
                _meter.text = "<b>ELECTROMETER</b>\nV  " + CrystalFx.FormatVolts(v) + "\npeak " + CrystalFx.FormatVolts(peak)
                    + "\nF  " + _piezo.StaticForce.ToString("0") + " N" + (spark ? "\n<color=#FFD24A>SPARK!</color>" : "")
                    + (_driveMode > 0 ? "\ndL " + (dl * 1e12).ToString("0.00") + " pm" : "");
            }
            if (_bar != null)
            {
                float frac = Mathf.Clamp01((float)(Math.Abs(_piezo.PeakVoltage) / _piezo.BreakdownV));
                _bar.localScale = new Vector3(Mathf.Max(0.001f, 0.3f * frac), 0.012f, 0.004f);
                _bar.localPosition = OnTop(0.05f - 0.15f + 0.15f * frac, 0.025f, 0.142f);
            }
        }

        /// <summary>Divide an ultrasonic frequency by 2^k (like a counter chain) until it is in the 110-440 Hz band.</summary>
        public static double Audible(double f)
        {
            if (f <= 0) return 0;
            while (f > 440.0) f *= 0.5;
            while (f < 110.0) f *= 2.0;
            return f;
        }

        public static int DivideSteps(double f)
        {
            int k = 0;
            while (f > 440.0) { f *= 0.5; k++; }
            return k;
        }

        protected override CrystalGuideCard.Content GetContent()
        {
            return new CrystalGuideCard.Content
            {
                Title = "1  PIEZOELECTRICITY  - squeeze a crystal, get a voltage",
                ChallengeId = ChallengeId,
                What =
                    "Some crystals have no centre of symmetry. Squeeze one and its positive and negative ions shift by different amounts, "
                    + "so each tiny cell becomes a little electric dipole. Billions of them add up to charge on the faces: a voltage you can measure.\n\n"
                    + "Quartz does this weakly but very stably (it times your watch). The ceramic PZT does it ~160x more strongly (it is the spark in a BBQ lighter).\n\n"
                    + "It works backwards too: put a voltage on the crystal and it changes shape (the converse effect, used in speakers, buzzers and ultrasound).\n\n"
                    + "<color=#9FB4C4>Model: single-axis lumped element, uniform stress, ideal electrodes; air gap uses the 3 kV/mm rule of thumb.</color>",
                Math =
                    "<b>Charge from force</b>   Q = d x F\n"
                    + "<b>Voltage</b>   V = Q / C,   C = eps0 epsR A / t + C_load\n"
                    + "<b>Leak</b>   V(t) = V0 exp(-t / RC)   (R_leak = 10^13 ohm here)\n"
                    + "<b>Strike</b>   F ~ m v / t_contact   (t_contact = 0.5 ms assumed)\n"
                    + "<b>Spark</b>   V > E_bd x gap = 3 MV/m x 1 mm = 3 kV\n"
                    + "<b>Converse</b>   dL = d x V  (x Q at resonance, f = N / t)\n\n"
                    + "quartz d11 = 2.3 pC/N, epsR = 4.5, element 10 x 10 x 5 mm (shown x4)  [IEEE Std 176-1987; Bechmann 1958]\n"
                    + "PZT-5A d33 = 374 pC/N, epsR ~ 1700, 2.6 mm x 6 mm igniter cylinder  [Morgan/APC datasheets]\n"
                    + "eps0 = 8.854e-12 F/m [CODATA]   quartz N = 2860 Hz m (f = 572 kHz), Q ~ 10^4 assumed",
                TryThis =
                    "1. Twist the press wheel clockwise (or tap Press +). Watch V climb as the platen bites (~0.8 V per newton for quartz).\n"
                    + "2. Hold the force still: V drains away (tau = RC ~ 28 s). Now release: V goes NEGATIVE. Piezo measures changes.\n"
                    + "3. Open the press, grab the hammer and hit the crystal hard. A 0.45 kg hammer at 5 m/s gives ~4.5 kN, ~3.7 kV: a spark!\n"
                    + "4. Turn the gas ON (red valve), then strike again: the spark lights the burner.\n"
                    + "5. Switch Material to PZT: a gentle tap now sparks. Try Drive (converse effect) and hear the resonance.\n"
                    + "6. Toolbelt CRYSTAL > Igniter: a real click lighter, same physics in your hand.",
                Lattice =
                    "Quartz (SiO2) is built from Si(+) and O(-) arranged in helices. Looking down the electric axis, each ring is a hexagon of alternating + and - (model, right).\n\n"
                    + "Unsqueezed, the centre of + charge sits exactly on the centre of - charge: no dipole.\n"
                    + "Squeeze along x: the ring flattens, the + sublattice slides one way and the - sublattice the other. The centres separate -> a dipole p (cyan arrow). "
                    + "Inside the crystal neighbouring dipoles cancel, but at the faces they do not: that uncovered 'bound charge' is what the electrodes collect.\n\n"
                    + "Converse: an applied field pushes + and - ions apart the same way, so the ring - and the crystal - changes shape.\n"
                    + "<color=#9FB4C4>Displacements in the model are exaggerated ~10^5 x (real ones are femtometres per newton).</color>"
            };
        }

        protected override void AddControls(CrystalGuideCard card)
        {
            card.AddControl("Press +", () => { _dial.Set(_dial.Value01 + 0.08f); });
            card.AddControl("Press -", () => { _dial.Set(_dial.Value01 - 0.08f); });
            card.AddControl("Release", () => { _dial.Set(0f); });
            card.AddControl("Strike soft", () => _piezo.Strike(0.45 * 2.0 / PiezoCrystal.ContactTime, _crystalVis.position));
            card.AddControl("Strike hard", () => _piezo.Strike(0.45 * 5.0 / PiezoCrystal.ContactTime, _crystalVis.position));
            _gasChip = card.AddControl("Gas: OFF", ToggleGas);
            _matChip = card.AddControl("Material: Quartz", () =>
            {
                _piezo.Pzt = !_piezo.Pzt;
                _piezo.ResetCharge();
                if (_crystalR != null) _crystalR.sharedMaterial = _piezo.Pzt ? _pztMat : _quartzMat;
                CrystalGuideCard.SetChipLabel(_matChip, _piezo.Pzt ? "Material: PZT" : "Material: Quartz");
            });
            _driveChip = card.AddControl("Drive: Off", () =>
            {
                _driveMode = (_driveMode + 1) % 4;
                string[] names = { "Drive: Off", "Drive: 100 V DC", "Drive: 1 kHz", "Drive: Reson." };
                CrystalGuideCard.SetChipLabel(_driveChip, names[_driveMode]);
            });
            card.AddControl("Hammer home", () =>
            {
                var h = UnityEngine.Object.FindAnyObjectByType<CrystalHammer>(FindObjectsInactive.Exclude);
                if (h != null) { var r = h.GetComponent<CrystalHomeReturn>(); if (r != null) r.PutBack(); }
            });
            card.AddControl("Zero meter", () => _piezo.ResetCharge());
        }

        protected override string LiveLine()
        {
            var p = _piezo;
            string s = p.MaterialName + ":  F " + p.StaticForce.ToString("0") + " N   Q " + CrystalFx.FormatCharge(p.Charge)
                + "   C " + CrystalFx.FormatCap(p.Capacitance) + "   V " + CrystalFx.FormatVolts(p.Voltage)
                + "   peak " + CrystalFx.FormatVolts(p.PeakVoltage) + "   gap " + CrystalFx.FormatVolts(p.BreakdownV);
            if (p.LastStrikeForce > 0)
                s += "\nlast strike F ~ " + p.LastStrikeForce.ToString("0") + " N -> " + CrystalFx.FormatVolts(p.D * p.LastStrikeForce / p.Capacitance);
            if (_driveMode == 3)
                s += "\nresonance f = N/t = " + (p.ResonanceHz / 1e3).ToString("0") + " kHz  (heard / 2^" + DivideSteps(p.ResonanceHz) + ")";
            s += "   burner " + (_burner.Lit ? "<color=#FFB84A>LIT</color>" : (_burner.Gas ? "gas on" : "off"));
            return s;
        }
    }
}
