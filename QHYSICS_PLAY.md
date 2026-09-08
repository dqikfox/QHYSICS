# QHYSICS ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â how to Play (Editor)

**Daily testing = Ctrl+P (Play).** Do **not** use Ctrl+B for daily runs ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â that builds an APK/player.
**Ctrl+P ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â°Ãƒâ€šÃ‚Â  APK.** Headset view needs **Meta Quest Link** (or Device Simulator on desktop).

## One Editor only

- Project: `C:\Users\KING\projects\QHYSICS`
- Branch: `reality-engine`
- Unity: **6000.7.0a4** (one instance ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â never a second Editor on this project)

## Who is the player?

**XR Origin IS the player** (CharacterController + Camera Offset + Main Camera + XRI hands).

On **desktop** (Editor Ctrl+P, no Quest Link / no running XR display):
- A simple **DesktopBody** (torso + head + hand proxies) stands under XR Origin - not parented to Main Camera / never under Giza pyramids.
- Head uses layer 31 so your own camera does not draw it (first-person).
- Hand proxies sit in front of the view; **E / LMB grab** attaches held props to the right-hand attach point.
- Bottom **hotbar 1-9** is the desktop inventory (VR toolbelt still works via M / Tab).

On **Quest Link**: desktop body hides; XR locomotion + Left/Right Hand Direct/Ray interactors take over. Light `XrControllerProxy` grips show on each controller (Hand Presence still used when OpenXR devices match). Grab attach is on each hand's `Attach` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â desktop HandAttach does not fight XR grabs. If the headset shows nothing, fix Link ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â you are not missing a character mesh.


## Play steps ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â Meta Quest Link (Quest 3S)

1. On PC: install/open **Meta Quest Link** (Air Link or cable). Quest in Developer Mode, same account, PC allowed.
2. Put on headset ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ enable **Link** / connect to this PC. Confirm Link status is Connected.
3. In Unity (only one Editor): open `Assets/Scenes/Faraday.unity`.
4. Optional once: **Reality Engine ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Fix Player Spawn** (or **Reset Player at Lab**) then **Ctrl+S** ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â parks XR Origin on LabPlaza north of the circuit table facing Khufu, enables Main Camera, wires locomotion XR Origin.
5. Press **Ctrl+P** (Play). Game view mirrors the HMD via OpenXR + Link.
6. First run: world **Main Menu ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Enter Sandbox** (or skip if already entered).
7. Interact: teleport / smooth move on plaza, grab circuit parts, Toolbelt **M / Tab / Menu**, Pause **Esc / P**.

If Play works in Editor Game view but the headset stays on the Quest home / black: Link is not active ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â fix Link first. Do not build APK for daily testing.


## Play steps ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â XR Device Simulator (desktop, no headset)

1. Package Manager ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ **XR Interaction Toolkit** ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Samples ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ import **XR Device Simulator** (once).
2. Project Settings ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ **XR Plug-in Management** ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ **XR Interaction Toolkit** ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ enable **Use XR Device Simulator in scenes** / auto-instantiate (or add the `XR Device Simulator` prefab to Faraday).
   - Asset: `Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â set simulator prefab after sample import; `Automatically Instantiate Simulator Prefab` can be on for Editor-only.
3. Open Faraday ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ **Ctrl+P**. Use keyboard/mouse per simulator HUD (move/look/grip).
4. Optional: **Reality Engine ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Fix Player Spawn** before Play if Origin pose looks wrong in Hierarchy.


## Play steps - Desktop keyboard (no headset, no Device Simulator)

Built-in fallback when **no XR display is running** (Editor Ctrl+P without Quest Link).

1. Open `Assets/Scenes/Faraday.unity`.
2. Optional once: **Reality Engine -> Ensure Player Character** (or Place Desktop Player; also auto-ensures on Play / Enter Sandbox).
3. Press **Ctrl+P**. Do **not** need Oculus Link or XR Device Simulator.
4. You should spawn on **LabPlaza**, see a simple standing body + FP hands, and a bottom hotbar 1-9.

### Keyboard map (desktop)

| Key | Action |
|-----|--------|
| **W A S D** | Move (CharacterController on XR Origin) |
| **Mouse** | Look (yaw Origin, pitch Main Camera) |
| **Shift** | Sprint |
| **Space** | Jump |
| **Ctrl** | Crouch |
| **H** | Recalibrate eye height (~1.65m Camera Offset / Floor) |
| **1-9** | Hotbar: Wire, Battery, Switch, Bulb, Resistor, Magnet, Field Lens, Cubit Rod, Delete |
| **Q** / **Scroll** | Cycle hotbar |
| **LMB** | Spawn selected part (or Delete when slot 8) |
| **E** / **LMB** on part | Grab |
| **LMB** / **Scroll** while holding CIRCUIT gadget (incl. Motor/Solar/Capacitor/Inductor/Diode/Fuse/Function Generator), Multimeter, Galvanometer, Oscilloscope, Frequency Counter, Power Meter, Flux Meter, Charge Meter, Voltmeter, Ammeter, Ohmmeter, Capacitance Meter, Inductance Meter, Resonance Meter, Impedance Meter, Power Factor Meter, Q Factor Meter, Admittance Meter, Decibel Meter, Crest Factor Meter, Energy Meter, Duty Cycle Meter, Slew Rate Meter, Probe, Stopwatch, or Field Lens |

### MEASURE Multimeter (CircuitLab fallback)
When no InductionCircuit is bound, Multimeter also reads the nearest placed CircuitLab component V/I (`GetVoltage` / `GetCurrentValue`) with honesty tag `[CircuitLab component]`. InductionCircuit still wins when present.
### CIRCUIT Resistor (hotbar 5)
1. Enter Sandbox / Induction.
2. Hotbar **5** (or toolbelt) spawn **Resistor**; grab it.
3. Hold near Coil; **N/P**, VR trigger, or desktop **LMB/scroll** cycles 2 / 8 / 50 / OPEN Ohm classical R_load.
4. Pair with Magnet sweep + Multimeter **I** / Lamp glow.
 Activate / cycle (desktop trigger; Multimeter pages; Probe NEAREST/ALL/COMP; Stopwatch LMB toggle / scroll-reset; Field Lens layer peel) |
| **F** / **RMB** | Drop |
| **R** | Throw |
| **Esc** / **P** | Pause (releases cursor) - Resume re-locks |
| **M** / **Tab** | Toolbelt (worn at hip/chest with lag ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â not glued to camera) |

When a Quest Link headset is connected / XR display running, desktop locomotion disables and XR continuous-move + snap turn own the CharacterController (no fight with WASD). Toolbelt follows HipAnchor on DesktopBody / XR Origin. Enter Sandbox / Fix Player Spawn recalibrates Floor + eye height.

### CIRCUIT Motor (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD Ã¢â€ â€™ Motor**; grab it.
3. Hold near Coil; rotor spins from classical |I| (pair with Battery and/or Magnet sweep + Resistor).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles gear 0.5x / 1x / 2x / REV. Honesty: kinematic spin proxy, not torque/back-EMF.

### CIRCUIT Solar (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Solar**; grab it.
3. Hold near Coil; classical series EMF from irradiance preset (pair with Lamp/Motor/Resistor/Multimeter).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles OFF / Dawn / Noon / Bright (0 / 1.5 / 3 / 6 V). Honesty: lumped photocurrent/irradiance EMF proxy â€” NOT a real PV I-V curve, not quantum, not MPPT.

### CIRCUIT Capacitor (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Capacitor**; grab it.
3. Hold near Coil; classical series C (pair with Battery + Resistor/Lamp; Multimeter I falls as Vc rises).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles SHORT / 1mF / 10mF / 100mF. Honesty: lumped RC only â€” NOT dielectric physics, not ESR/ESL. **New Run** clears Vc.

### CIRCUIT Inductor (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Inductor**; grab it.
3. Hold near Coil; classical series L (pair with Battery + Resistor/Lamp; Multimeter I ramps; with Capacitor = RLC).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles SHORT / 1mH / 10mH / 100mH. Honesty: lumped RL/RLC only â€” NOT core saturation, not skin effect, not mutual M. **New Run** clears I_L (and Vc).

### CIRCUIT Diode (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Diode**; grab it.
3. Hold near Coil; ideal series diode clamp (pair with Magnet sweep + Multimeter **I** / Lamp; I one-sided).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles SHORT / FWD / REV. Honesty: ideal half-wave only â€” NOT Shockley equation, not recovery, not avalanche; inductive kick not snubbered.

### CIRCUIT Fuse (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Fuse**; grab it.
3. Hold near Coil; ideal |I| trip open (pair with Magnet sweep + Multimeter **I** / Lamp; hard sweep blows fuse and I collapses).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles 5mA / 20mA / 50mA / BYPASS. While **BLOWN**, activate / **N** rearms. Honesty: ideal |I| threshold only â€” NOT I2t, not arc, not thermal model.

### CIRCUIT LED (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> LED**; grab it.
3. Hold near Coil; ideal series diode + colored glow (pair with Magnet sweep or Battery + Resistor; Multimeter **I** one-sided on color presets).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles RED / GREEN / BLUE / SHORT. Honesty: ideal diode + emission proxy ï¿½ NOT bandgap photons, not real LED I-V, not thermal.

### CIRCUIT Speaker (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Speaker**; grab it.
3. Hold near Coil; hear classical |I| as a procedural sine (pair with Magnet sweep or Battery + Resistor; Multimeter **I**).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles MUTE / LO / MID / HI gain. Honesty: sine |I| proxy - NOT a real voice coil, not Lorentz force audio, not AC spectrum.

### CIRCUIT Potentiometer (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Potentiometer**; grab it.
3. Hold near Coil; applies classical series R_load via discrete wiper presets (pair with Battery + Lamp/Motor/Multimeter).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles 1Î© / 5Î© / 10Î© / 25Î© / 100Î© / 1kÎ© (default 10Î©). Honesty: discrete lumped R_load wiper steps â€” NOT a real potentiometer, not 3-terminal divider, not taper curve. Overrides Resistor when both near; Switch OPEN still wins.

### CIRCUIT Transformer (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Transformer**; grab it.
3. Hold near Coil; applies discrete lumped turns N (pair with Magnet sweep + Multimeter V/I).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles 1:4 / 1:2 / 1:1 / 2:1 / 4:1 (N=20/40/80/160/320, default 1:1). Honesty: lumped turns tap (EMF=-N dPhi/dt) - NOT mutual inductance, not dual-winding transformer, not core hysteresis.


### MEASURE Galvanometer (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Galvanometer**; grab it.
3. Hold near Coil; needle deflects from classical signed I (pair with Magnet sweep or Battery + Resistor; Multimeter I for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles 1mA / 5mA / 20mA / 100mA full-scale (default 5mA). Honesty: ideal signed needle from InductionCircuit I - NOT coil torque dynamics, not damping, not shunt burden.


### MEASURE Oscilloscope (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Oscilloscope**; grab it.
3. Hold near Coil; strip chart samples classical Emf / I / Phi (pair with Magnet sweep or Battery + Resistor; Multimeter for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles EMF0.5 / EMF1 / EMF2 / I0.5 / I1 / I2 / Phi1 / Phi2 (default EMF1 = EMF 1s). Honesty: ideal rolling strip from classical InductionCircuit Emf/I/Phi samples â€” NOT real ADC, not triggered scope, not FFT, not probe capacitance.





### MEASURE Frequency Counter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Frequency Counter**; grab it.
3. Hold near Coil; gate Hz from rising zero-cross of classical Emf / I (pair with BUILD Function Generator or Magnet sweep; Oscilloscope for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles EMF0.5 / EMF1 / EMF2 / I0.5 / I1 / I2 (default EMF1 = EMF 1s gate). Honesty: ideal rising zero-cross gate from InductionCircuit Emf/I - NOT real counter/timer, not PLL, not FFT, not Schmitt hysteresis.

### MEASURE Power Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Power Meter**; grab it.
3. Hold near Coil; classical P_load (I^2 R_load), Emf*I, |P|, or integral P dt energy (pair with BUILD Function Generator / Battery / Magnet; Multimeter / Oscilloscope for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles LOAD / EI / ABS / ENERGY / CLR (default LOAD; CLR zeros joules then snaps to ENERGY). Honesty: ideal lumped wattmeter from InductionCircuit - NOT real thermocouple/Hall/analog-multiplier meter, not true RMS, not PF, not shunt burden.

### MEASURE Flux Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Flux Meter**; grab it.
3. Hold near Coil; classical Phi (Wb), dPhi/dt (Wb/s), or peak |Phi| hold (pair with PHYSICS Magnet sweep; Oscilloscope Phi / Multimeter for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles PHI / DPHI / PEAK / CLR (default PHI; CLR zeros peak then snaps to PHI). Honesty: ideal lumped fluxmeter from InductionCircuit - NOT real integrating fluxmeter, not search-coil ballistic galvo, not Hall BÂ·A, not hysteresis tracer.

### MEASURE Charge Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Charge Meter**; grab it.
3. Hold near Coil; classical Q=âˆ«I dt (C), peak |Q|, or Iavg (=Q/t since CLR) (pair with BUILD Capacitor / Function Generator / Magnet; Multimeter I / Power Meter for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles Q / PEAK / AVG / CLR (default Q; CLR zeros Q/peak/timer then snaps to Q). Honesty: ideal coulomb integrator from InductionCircuit I - NOT real electrometer, not Faraday cup, not Keithley charge amp, not dielectric absorption.



### MEASURE Ammeter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Ammeter**; grab it.
3. Hold near Coil; classical I (A), peak |I|, or Irms over window since CLR (pair with BUILD Function Generator / PHYSICS Magnet; Multimeter I / Galvanometer for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles I / PEAK / RMS / CLR (default I; CLR zeros peak/RMS window then snaps to I). Honesty: ideal CurrentAmperes from InductionCircuit - NOT real DMM ammeter, not shunt burden, not Hall-effect clamp, not true-RMS ADC.

### MEASURE Ohmmeter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Ohmmeter**; grab it.
3. Hold near Coil; classical Rtot (ohm), coil Rload, or Emf/I effective R (pair with BUILD Resistor / Function Generator / PHYSICS Magnet; Multimeter LOAD / Ammeter for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles R / LOAD / EFF / CLR (default R; CLR zeros peak |Reff| then snaps to R). Honesty: ideal TotalResistanceOhms / Coil.LoadResistance / Emf/I from InductionCircuit - NOT real DMM ohms mode, not Kelvin 4-wire, not wheatstone, not insulation megger.

### MEASURE Capacitance Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Capacitance Meter**; grab it.
3. Hold near Coil; classical series C (F), capacitor Vc, or energy 0.5*C*V^2 (pair with BUILD Capacitor / Battery / Function Generator; Multimeter / Charge Meter for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles C / VC / ENERGY / CLR (default C; CLR resets Vc then snaps to C). Honesty: ideal SeriesCapacitance / CapacitorVolts / 0.5CV^2 from InductionCircuit - NOT real LCR meter, not ESR bridge, not dielectric absorption analyzer, not impedance analyzer.

### MEASURE Inductance Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Inductance Meter**; grab it.
3. Hold near Coil; classical series L (H), inductor I_L, or energy 0.5*L*I^2 (pair with BUILD Inductor / Function Generator / PHYSICS Magnet; Ammeter / Capacitance Meter for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles L / IL / ENERGY / CLR (default L; CLR resets I_L then snaps to L). Honesty: ideal SeriesInductance / InductorCurrentAmperes / 0.5LI^2 from InductionCircuit - NOT real LCR meter, not Q-meter, not impedance analyzer, not mutual inductance meter.
### MEASURE Resonance Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Resonance Meter**; grab it.
3. Hold near Coil; classical LC resonant f0=1/(2pi sqrt(LC)), omega0, or L/C (pair with BUILD Inductor + Capacitor / Function Generator; Inductance Meter / Capacitance Meter / Frequency Counter for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles F0 / OMEGA / LC / CLR (default F0; CLR resets energy storage Vc+I_L then snaps to F0). Honesty: ideal SeriesInductance / SeriesCapacitance from InductionCoil - NOT real network analyzer, not swept VNA, not Q from -3dB bandwidth, not impedance analyzer.
### MEASURE Impedance Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Impedance Meter**; grab it.
3. Hold near Coil; classical series RLC |Z|, reactance X, phase PHI, or Emf/I effective |Z| (pair with BUILD Inductor + Capacitor / Function Generator; Resonance Meter / Frequency Counter / Ohmmeter for cross-check). Drive omega from Emf rising zero-cross gate; if no valid Hz yet and L,C>0, model Z uses omega0=1/sqrt(LC) fallback (honesty-labeled).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles Z / X / PHI / EFF / CLR (default Z; CLR zeros peak |Zeff|, resets energy storage Vc+I_L, then snaps to Z). Honesty: ideal series RLC impedance from classical InductionCircuit R / Series L / Series C at estimated omega (or omega0 fallback) - NOT real VNA, not impedance analyzer, not LCR bridge, not Kelvin, not true complex Z from FFT.

### MEASURE Power Factor Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Power Factor Meter**; grab it.
3. Hold near Coil; classical series RLC PF=R/|Z| (LEAD/LAG/UNITY), phase PHI, reactive VAR=I^2*X, or apparent VA (pair with BUILD Inductor + Capacitor / Function Generator; Impedance Meter / Power Meter / Resonance Meter for cross-check). Drive omega from Emf rising zero-cross gate; if no valid Hz yet and L,C>0, model uses omega0=1/sqrt(LC) fallback (honesty-labeled).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles PF / PHI / VAR / VA / CLR (default PF; CLR zeros peak |VAR|, resets energy storage Vc+I_L, then snaps to PF). Honesty: ideal series RLC power factor from classical InductionCircuit R / Series L / Series C at estimated omega (or omega0 fallback) - NOT real PF meter, not wattmeter/VAR transducer, not true-RMS, not phase-locked, not FFT.
### MEASURE Q Factor Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Q Factor Meter**; grab it.
3. Hold near Coil; classical series RLC Q0=(1/R)sqrt(L/C), bandwidth BW=f0/Q0, damping zeta=1/(2Q0), or ENERGY 0.5LI^2+0.5CV^2 (pair with BUILD Inductor + Capacitor; Resonance Meter / Impedance Meter / Power Factor Meter for cross-check). Needs R,L,C all > 0.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles Q / BW / ZETA / ENERGY / CLR (default Q; CLR zeros peak Q0, resets energy storage Vc+I_L, then snaps to Q). Honesty: ideal series RLC quality factor from classical InductionCircuit R / Series L / Series C - NOT real Q-meter, not network analyzer, not 3dB bandwidth sweep, not ring-down, not FFT.

### MEASURE Admittance Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Admittance Meter**; grab it.
3. Hold near Coil; classical series RLC |Y|=1/|Z|, conductance G=R/|Z|^2, susceptance B=-X/|Z|^2, or phase PHI (pair with BUILD Inductor + Capacitor / Function Generator; Impedance Meter / Resonance Meter / Frequency Counter for cross-check). Drive omega from Emf rising zero-cross gate; if no valid Hz yet and L,C>0, model uses omega0=1/sqrt(LC) fallback (honesty-labeled).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles Y / G / B / PHI / CLR (default Y; CLR zeros peak |Y|, resets energy storage Vc+I_L, then snaps to Y). Honesty: ideal series RLC admittance Y=1/Z from classical InductionCircuit R / Series L / Series C at estimated omega (or omega0 fallback) - NOT real admittance bridge, not network analyzer, not VNA Y-params, not FFT complex Y.

### MEASURE Decibel Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Decibel Meter**; grab it.
3. Hold near Coil; classical logarithmic ratios of Emf / I / LoadPower (pair with BUILD Function Generator / Battery; Power Meter / Voltmeter / Ammeter for cross-check). Refs: dBV vs 1 V, dBI vs 1 A, dBW vs 1 W, dBm vs 1 mW.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles DBV / DBI / DBW / DBM / CLR (default DBV; CLR zeros peak tracker, then snaps to DBV). Honesty: ideal lumped dB from classical InductionCircuit samples - NOT calibrated SPL, not A-weighting, not true-RMS audio meter, not spectrum analyzer dB, not antenna gain.

### MEASURE Crest Factor Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Crest Factor Meter**; grab it.
3. Hold near Coil; classical windowed peak/RMS crest for Emf (CFV) and I (CFI), plus Emf form factor FF=RMS/mean|Emf| (pair with BUILD Function Generator SIN/SQR/TRI; Voltmeter / Ammeter / Decibel Meter for cross-check). Expect ~1.414 sine CF, ~1 square CF, ~1.732 triangle CF once the window fills.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles CFV / CFI / FF / CLR (default CFV; CLR zeros the peak/RMS/mean window, then snaps to CFV). Honesty: ideal windowed peak and RMS from classical InductionCircuit Emf / I - NOT true-RMS ADC, not IEC crest-factor meter, not THD, not scope math, not calibrated waveform analyzer.

### MEASURE Energy Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Energy Meter**; grab it.
3. Hold near Coil; classical energy integrator E=∫P dt from LoadPowerWatts, plus live P and window AVG=E/t (pair with BUILD Function Generator / Battery; Power Meter / Decibel Meter for cross-check). Energy accumulates while linked and within tip range.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles E / P / AVG / CLR (default E; CLR zeros the integrator and window, then snaps to E). Honesty: ideal time integral of lumped InductionCircuit LoadPowerWatts - NOT a real watt-hour meter, not calibrated energy logger, not four-quadrant / true-RMS joule hardware.

### MEASURE Duty Cycle Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Duty Cycle Meter**; grab it.
3. Hold near Coil; classical windowed positive duty for Emf (DUTY) and I (DUTYI), plus period T from rising Emf zero-crosses (pair with BUILD Function Generator SQR/SIN/TRI; Frequency Counter / Oscilloscope for cross-check). Expect ~50% for symmetric bipolar sine/square once the window fills.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles DUTY / DUTYI / T / CLR (default DUTY; CLR zeros the high-time window and period gate, then snaps to DUTY). Honesty: ideal sample high-fraction and zero-cross gate from classical InductionCircuit Emf / I - NOT a real duty-cycle meter, not oscilloscope duty, not PWM analyzer, not calibrated pulse-width hardware.

### MEASURE Slew Rate Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Slew Rate Meter**; grab it.
3. Hold near Coil; classical finite-diff peak |dEmf/dt| (SRV), peak |dI/dt| (SRI), and live |dEmf/dt| (LIVE) (pair with BUILD Function Generator TRI/SQR edges; Oscilloscope / Duty Cycle Meter / Crest Factor for cross-check). Sharp edges should spike SRV.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles SRV / SRI / LIVE / CLR (default SRV; CLR zeros peaks and the prev-sample gate, then snaps to SRV). Honesty: ideal sample-to-sample derivatives from classical InductionCircuit Emf / I - NOT a real slew-rate meter, not scope cursors, not op-amp SR datasheet instrument, not calibrated dV/dt probe hardware.

### MEASURE Voltmeter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Voltmeter**; grab it.
3. Hold near Coil; classical Emf (V), peak |Emf|, or Vrms over window since CLR (pair with BUILD Function Generator / PHYSICS Magnet; Multimeter Emf / Oscilloscope EMF for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles V / PEAK / RMS / CLR (default V; CLR zeros peak/RMS window then snaps to V). Honesty: ideal EmfVolts from InductionCircuit - NOT real DMM, not true-RMS ADC, not high-Z probe, not differential/isolated meter.
### CIRCUIT Function Generator (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Function Generator**; grab it.
3. Hold near Coil; drives classical ideal series EMF waveform (pair with MEASURE Oscilloscope / Frequency Counter / Power Meter / Flux Meter / Charge Meter / Voltmeter / Ammeter / Ohmmeter / Capacitance Meter / Inductance Meter / Resonance Meter / Impedance Meter / Power Factor Meter / Q Factor Meter / Admittance Meter / Decibel Meter / Crest Factor Meter / Energy Meter / Duty Cycle Meter / Slew Rate Meter / Multimeter / Galvanometer / Lamp).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles OFF / SIN1 / SIN5 / SIN10 / SQR5 / SQR10 / TRI5 / TRI10 (default SIN5 = 5 Hz @ 1.5 Vpk). Honesty: ideal AWG series EMF on InductionCoil - NOT real DDS, not output impedance, not coil frequency response, not sync/trigger.
## Play steps ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â checklist

1. Open `Assets/Scenes/Faraday.unity` (Build Settings already has Faraday enabled).
2. Confirm Hierarchy has **LabLandscape** (Giza) and **QhysicsUI** (or RealityEngine host). If missing:
   - Menu **Reality Engine ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Place Giza Complex**
   - Menu **Reality Engine ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Place QHYSICS UI**
   - **Ctrl+S** / File ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Save so placement persists.
3. Optional: **Reality Engine ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Fix Player Spawn** (XR Origin on plaza, Main Camera MainCamera+enabled, facing Khufu).
4. Press **Ctrl+P** (or the Play button).
5. Headset: Quest Link / OpenXR, **or** Editor with **XR Device Simulator**.
6. First run: world **Main Menu ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Enter Sandbox** (or skip if already entered). Short onboarding tips are dismissible (Next / Skip).
7. Interact:
   - Move with XRI locomotion / teleport on plaza
   - Grab circuit parts from table dispensers (grip)
   - Build Battery ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Wire ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Bulb ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Switch loop
   - **M / Tab / Menu / B / Grip** toggles Toolbelt
   - **Esc / P** opens Pause ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Resume / Reset Experiment / Settings / Exit Play (Editor)
   - SimChip (`> 1x`) cycles sim speed / pause / reset (Reset also calls CircuitLab.Reset)

## Shipping only

- **Ctrl+B** / Build and Run ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Android (Quest) or Windows player.
- Quest: Developer Mode + authorized `adb devices` before APK install.
- Do **not** build APK unless `adb devices` shows the authorized Quest serial you intend to flash.

## Never

- Second Unity on this project
- `GetInstanceID` in tooling paths
- `Sprites/Default` for Giza/lab surfaces
- Disabling MountainScene
- Assuming Ctrl+P installs to the headset (it does not)

