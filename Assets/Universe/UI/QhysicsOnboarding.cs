using UnityEngine;
using TMPro;

namespace RealityEngine.UI
{
    /// <summary>
    /// Short dismissible world-space tips: Welcome ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ Grab ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ Try table. Not a trap.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(205)]
    public sealed class QhysicsOnboarding : MonoBehaviour
    {
        public const string RootName = "QhysicsOnboarding";
        const string PrefKey = "QHYSICS.Onboarding.Dismissed";

        static readonly string[] Tips =
        {
            "Welcome to QHYSICS\nCircuit lab on the table. Giza outside.",
            "Desktop: WASD move, mouse look, E/LMB grab, F drop, R throw.\nVR: grip / trigger. Hotbar 1-9 / scroll (slot 5 = CIRCUIT Resistor).",
            "You are the XR Origin (and desktop body when no headset). Controllers / DesktopBody are your hands - not a separate avatar.",
            "Try a loop: Battery -> Wire -> Bulb -> Switch.\nEsc/P pause. Toolbelt New Run resets the table and clears spawned gadgets.",
            "PHYSICS Field Lens: grab it; N/P, VR trigger, or desktop LMB/scroll peels layers (honesty tags on readout).",
            "PHYSICS Magnet/Dipole: grab a live classical MagneticDipole (Probe reads B).",
            "PHYSICS Coil: grab a live classical InductionCoil; move a Magnet through it (Multimeter reads Emf/I).",
            "MEASURE Multimeter: binds nearest InductionCircuit (or nearest CircuitLab component V/I if none); N/P, VR trigger, or desktop LMB/scroll cycles ALL/EMF/I/LOAD pages.",
            "Lab + PHYSICS coils auto-bind every MagneticDipole ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â gadget Magnet through lab Coil still drives Emf/I.",
            "CIRCUIT Resistor: hold near a Coil to apply classical R_load (N/P or VR trigger cycles 2/8/50/OPEN); Multimeter I changes.",
            "CIRCUIT Motor: hold near a Coil Ã¢â‚¬â€ spins from classical |I| (N/P or VR trigger cycles gear 0.5x/1x/2x/REV; pair with Battery/Resistor/Magnet).",
            "CIRCUIT Solar: hold near a Coil â€” N/P or VR trigger cycles OFF/Dawn/Noon/Bright irradiance EMF (classical photocurrent proxy; not PV I-V/MPPT).",
            "CIRCUIT Capacitor: hold near a Coil â€” N/P or VR trigger cycles SHORT/1mF/10mF/100mF (lumped RC; Vc charges from Battery/EMF; New Run clears Vc).",
            "CIRCUIT Inductor: hold near a Coil â€” N/P or VR trigger cycles SHORT/1mH/10mH/100mH (lumped RL/RLC; I ramps; New Run clears I_L).",
            "CIRCUIT Diode: hold near a Coil â€” N/P or VR trigger cycles SHORT/FWD/REV (ideal half-wave of magnet-sweep EMF; Multimeter I one-sided).",
            "CIRCUIT Fuse: hold near a Coil â€” trips open when |I| exceeds 5/20/50mA (N/P cycle trip; activate while BLOWN rearms; ideal |I| trip not I2t).",
            "CIRCUIT LED: hold near a Coil ï¿½ N/P or VR trigger cycles RED/GREEN/BLUE/SHORT (ideal diode + colored |I| glow; not bandgap photons).",
            "CIRCUIT Speaker: hold near a Coil - hears classical |I| as procedural sine (N/P or VR trigger cycles MUTE/LO/MID/HI gain; not a real voice coil).",
            "CIRCUIT Potentiometer: hold near a Coil â€” N/P or VR trigger cycles 1/5/10/25/100/1k ohm discrete R_load wiper (NOT a real 3-terminal pot; overrides Resistor when both near; Switch OPEN still wins).",
            "CIRCUIT Transformer: hold near a Coil - N/P or VR trigger cycles 1:4/1:2/1:1/2:1/4:1 lumped turns tap (N=20/40/80/160/320; EMF=-N dPhi/dt; NOT mutual inductance / dual winding / core hysteresis).",
            "MEASURE Galvanometer: hold near a Coil - needle deflects from classical signed I (N/P or VR trigger cycles 1/5/20/100mA full-scale; ideal needle NOT coil torque / damping / shunt).",
            "MEASURE Oscilloscope: hold near a Coil - rolling strip of classical Emf/I/Phi (N/P or VR trigger cycles EMF0.5/EMF1/EMF2/I0.5/I1/I2/Phi1/Phi2; ideal strip NOT ADC / triggered scope / FFT / probe C).",
            "CIRCUIT Function Generator: hold near a Coil - drives classical ideal series EMF waveforms (N/P or VR trigger cycles OFF/SIN1/SIN5/SIN10/SQR5/SQR10/TRI5/TRI10; pair with Oscilloscope; NOT real DDS/AWG/Zout/sync).",
            "MEASURE Frequency Counter: hold near a Coil - gate Hz from rising zero-cross of classical Emf/I (N/P or VR trigger cycles EMF0.5/EMF1/EMF2/I0.5/I1/I2; pair with Function Generator; NOT real counter/PLL/FFT).",
            "MEASURE Power Meter: hold near a Coil - classical P_load (I^2 R_load), Emf*I, |P|, or energy integral (N/P or VR trigger cycles LOAD/EI/ABS/ENERGY/CLR; CLR zeros joules; NOT real thermocouple/Hall/true-RMS/PF meter).",
            "MEASURE Flux Meter: hold near a Coil - classical Phi (Wb), dPhi/dt, or peak |Phi| (N/P or VR trigger cycles PHI/DPHI/PEAK/CLR; CLR zeros peak; pair with Magnet sweep; NOT real integrating fluxmeter / search-coil / Hall BÂ·A).",
            "MEASURE Charge Meter: hold near a Coil - classical Q=integral(I) dt, peak |Q|, or Iavg (N/P or VR trigger cycles Q/PEAK/AVG/CLR; CLR zeros Q; pair with Capacitor / Function Generator; NOT real electrometer / Faraday cup / Keithley).",
            "MEASURE Voltmeter: hold near a Coil - classical Emf (V), peak |Emf|, or Vrms window (N/P or VR trigger cycles V/PEAK/RMS/CLR; CLR zeros peak/RMS; pair with Function Generator / Magnet; NOT real DMM / true-RMS ADC / high-Z probe).",
            "MEASURE Ammeter: hold near a Coil - classical I (A), peak |I|, or Irms window (N/P or VR trigger cycles I/PEAK/RMS/CLR; CLR zeros peak/RMS; pair with Function Generator / Magnet / Galvanometer; NOT real DMM ammeter / shunt / Hall clamp / true-RMS ADC).",
            "MEASURE Ohmmeter: hold near a Coil - classical Rtot (ohm), Rload, or Emf/I (N/P or VR trigger cycles R/LOAD/EFF/CLR; CLR zeros peak |Reff|; pair with Resistor / Function Generator / Magnet; NOT real DMM ohms / Kelvin 4-wire / wheatstone / megger).",
            "MEASURE Capacitance Meter: hold near a Coil - classical series C (F), Vc, or 0.5*C*V^2 (N/P or VR trigger cycles C/VC/ENERGY/CLR; CLR resets Vc; pair with Capacitor / Battery / Function Generator; NOT real LCR / ESR bridge / dielectric absorption / impedance analyzer).",
            "MEASURE Inductance Meter: hold near a Coil - classical series L (H), I_L, or 0.5*L*I^2 (N/P or VR trigger cycles L/IL/ENERGY/CLR; CLR resets I_L; pair with Inductor / Function Generator / Magnet; NOT real LCR / Q-meter / impedance analyzer / mutual inductance).",
            "MEASURE Resonance Meter: hold near a Coil - classical LC f0=1/(2pi sqrt(LC)), omega0, or L/C (N/P or VR trigger cycles F0/OMEGA/LC/CLR; CLR resets Vc+I_L; pair with Inductor / Capacitor / Function Generator; NOT real VNA / network analyzer / Q-bandwidth / impedance analyzer).",
            "MEASURE Impedance Meter: hold near a Coil - classical series RLC |Z|, X, phase, or Emf/I (N/P or VR trigger cycles Z/X/PHI/EFF/CLR; CLR zeros peak |Zeff| + resets Vc+I_L; pair with Inductor / Capacitor / Function Generator / Frequency Counter; NOT real VNA / impedance analyzer / LCR bridge / Kelvin / FFT Z).",
            "MEASURE Power Factor Meter: hold near a Coil - classical series RLC PF=R/|Z|, phase, VAR=I^2*X, or VA (N/P or VR trigger cycles PF/PHI/VAR/VA/CLR; CLR zeros peak |VAR| + resets Vc+I_L; pair with Inductor / Capacitor / Function Generator / Impedance Meter; NOT real PF meter / wattmeter-VAR transducer / true-RMS / PLL / FFT).",
            "MEASURE Q Factor Meter: hold near a Coil - classical series RLC Q0=(1/R)sqrt(L/C), BW=f0/Q0, zeta=1/(2Q0), or ENERGY (N/P or VR trigger cycles Q/BW/ZETA/ENERGY/CLR; CLR zeros peak Q0 + resets Vc+I_L; pair with Inductor / Capacitor / Resonance Meter / Impedance Meter; NOT real Q-meter / network analyzer / 3dB sweep / ring-down / FFT).",
            "MEASURE Admittance Meter: hold near a Coil - classical series RLC |Y|=1/|Z|, G=R/|Z|^2, B=-X/|Z|^2, or PHI (N/P or VR trigger cycles Y/G/B/PHI/CLR; CLR zeros peak |Y| + resets Vc+I_L; pair with Impedance Meter / Function Generator / Inductor / Capacitor; NOT real admittance bridge / VNA Y-params / network analyzer / FFT Y).",
            "MEASURE Decibel Meter: hold near a Coil - classical dBV / dBI / dBW / dBm from Emf / I / LoadPower vs 1V / 1A / 1W / 1mW (N/P or VR trigger cycles DBV/DBI/DBW/DBM/CLR; CLR zeros peak; pair with Function Generator / Power Meter / Voltmeter / Ammeter; NOT calibrated SPL / A-weighting / true-RMS audio / spectrum dB).",
            "MEASURE Crest Factor Meter: hold near a Coil - classical peak/RMS crest CFV / CFI and Emf form factor FF (N/P or VR trigger cycles CFV/CFI/FF/CLR; CLR zeros window; pair with Function Generator SIN/SQR/TRI / Voltmeter / Ammeter; NOT true-RMS ADC / IEC crest meter / THD / scope math).",
            "MEASURE Energy Meter: hold near a Coil - classical energy E=∫P dt from LoadPowerWatts plus live P and window AVG (N/P or VR trigger cycles E/P/AVG/CLR; CLR zeros integrator; pair with Power Meter / Function Generator / Battery; NOT watt-hour meter / calibrated logger / four-quadrant joule hardware).",
            "MEASURE Duty Cycle Meter: hold near a Coil - classical positive duty DUTY/DUTYI and Emf period T (N/P or VR trigger cycles DUTY/DUTYI/T/CLR; CLR zeros window; pair with Function Generator SQR / Frequency Counter / Oscilloscope; NOT duty-cycle meter / scope duty / PWM analyzer / pulse-width hardware).",
            "MEASURE Slew Rate Meter: hold near a Coil - classical peak |dEmf/dt| SRV / peak |dI/dt| SRI / live |dEmf/dt| (N/P or VR trigger cycles SRV/SRI/LIVE/CLR; CLR zeros peaks; pair with Function Generator TRI/SQR edges / Oscilloscope / Duty Cycle Meter; NOT slew-rate meter / scope cursors / op-amp SR datasheet / calibrated dV/dt probe).",
            "MEASURE Rise/Fall Meter: hold near a Coil - classical 10-90% Emf rise (RISE) / 90-10% fall (FALL) / live edge (LIVE) (N/P or VR trigger cycles RISE/FALL/LIVE/CLR; CLR zeros times; pair with Function Generator SQR/TRI edges / Oscilloscope / Slew Rate Meter; NOT rise-time meter / scope cursors / BER edge analyzer / calibrated pulse-edge hardware).",
            "MEASURE Overshoot Meter: hold near a Coil - classical Emf overshoot % (OSV) / I overshoot % (OSI) / peak |Emf| (PK) (N/P or VR trigger cycles OSV/OSI/PK/CLR; CLR zeros peaks/windows; pair with Function Generator SQR edges / Oscilloscope / Rise/Fall Meter; NOT overshoot meter / scope cursors / BER edge analyzer / calibrated pulse-edge / step-response hardware).",
            "CIRCUIT Bulb/Lamp: hold near a Coil â€” glows from classical |I| (pair with Magnet sweep + Resistor); Multimeter still reads Emf/I.",
            "CIRCUIT Switch: hold near a Coil ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â N/P or VR trigger toggles CLOSED/OPEN (OPEN = classical open circuit; kills Lamp |I|).",
            "CIRCUIT Battery: hold near a Coil ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â N/P or VR trigger cycles 1.5/3/6/9/OFF V (classical series EMF; Lamp glows without magnet sweep).",
            "CIRCUIT Wire: hold near a Coil ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â classical series jumper R (N/P or VR trigger cycles 0.05/0.2/1 ohm; Resistor/Switch OPEN override).",
            "MEASURE Probe: binds nearest MagneticDipole by tip; N/P, VR trigger, or desktop LMB/scroll cycles NEAREST/ALL/COMP pages.",
            "MEASURE Stopwatch: T/Y or VR trigger; desktop LMB toggles, scroll-down resets (wall-clock).",
            "Desktop: while holding Battery/Resistor/Switch/Wire/Motor/Solar/Capacitor/Inductor/Diode/Fuse/LED/Speaker/Potentiometer/Transformer/Function Generator/Galvanometer/Oscilloscope/Frequency Counter/Power Meter/Flux Meter/Charge Meter/Voltmeter/Ammeter/Ohmmeter/Capacitance Meter/Inductance Meter/Resonance Meter/Impedance Meter/Power Factor Meter/Q Factor Meter/Admittance Meter/Decibel Meter/Crest Factor Meter/Energy Meter/Duty Cycle Meter/Slew Rate Meter/Rise/Fall Meter/Overshoot Meter/Multimeter/Probe/Stopwatch/Field Lens, LMB or scroll activates (VR trigger). Crosshair turns amber when held.",
        };

        Canvas _canvas;
        TextMeshProUGUI _body;
        int _step;
        Camera _cam;
        bool _ready;

        public static QhysicsOnboarding Ensure(Transform parent)
        {
            Transform existing = parent != null ? parent.Find(RootName) : null;
            if (existing == null)
            {
                GameObject found = GameObject.Find(RootName);
                if (found != null)
                    existing = found.transform;
            }
            if (existing != null)
            {
                var c = existing.GetComponent<QhysicsOnboarding>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsOnboarding>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsOnboarding>();
            comp.Build();
            return comp;
        }

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(760f, 360f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(720f, 320f));
            QhysicsUiBuilder.LayoutVertical(face.rectTransform, 12f);

            QhysicsUiBuilder.Label(face.transform, "Title", "QHYSICS", QhysicsUiStyle.FontTitle,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.Center).rectTransform.sizeDelta = new Vector2(680f, 48f);

            _body = QhysicsUiBuilder.Label(face.transform, "Body", Tips[0], QhysicsUiStyle.FontBody,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.Center);
            _body.rectTransform.sizeDelta = new Vector2(660f, 140f);
            _body.textWrappingMode = TextWrappingModes.Normal;

            var row = new GameObject("Buttons", typeof(RectTransform));
            row.transform.SetParent(face.transform, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(680f, 88f);
            QhysicsUiBuilder.LayoutHorizontal(rowRt, 16f);

            QhysicsUiBuilder.ChipButton(row.transform, "Next", "Next", new Vector2(200f, 80f), Next);
            QhysicsUiBuilder.ChipButton(row.transform, "Skip", "Skip tips", new Vector2(200f, 80f), Dismiss);

            _step = 0;
            Refresh();
            _ready = true;

            bool dismissed = PlayerPrefs.GetInt(PrefKey, 0) == 1;
            if (dismissed || !Application.isPlaying)
                SetVisible(false);
            else
                SetVisible(true);
        }

        void LateUpdate()
        {
            if (!_ready || _canvas == null || !_canvas.gameObject.activeSelf)
                return;
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;
            Vector3 fwd = _cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 pos = _cam.transform.position + fwd * 1.15f + Vector3.up * 0.05f;
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
        }

        void Next()
        {
            _step++;
            if (_step >= Tips.Length)
            {
                Dismiss();
                return;
            }
            Refresh();
        }

        void Refresh()
        {
            if (_body != null && _step >= 0 && _step < Tips.Length)
                _body.text = Tips[_step];
        }

        public void Dismiss()
        {
            PlayerPrefs.SetInt(PrefKey, 1);
            PlayerPrefs.Save();
            SetVisible(false);
        }

        public void SetVisible(bool on)
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(on);
        }

        /// <summary>Editor / pause menu helper to show tips again.</summary>
        public void ResetAndShow()
        {
            PlayerPrefs.SetInt(PrefKey, 0);
            _step = 0;
            Refresh();
            SetVisible(true);
        }
    }
}

