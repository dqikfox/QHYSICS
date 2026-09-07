# QHYSICS ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â how to Play (Editor)

**Daily testing = Ctrl+P (Play).** Do **not** use Ctrl+B for daily runs ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â that builds an APK/player.
**Ctrl+P ÃƒÂ¢Ã¢â‚¬Â°Ã‚Â  APK.** Headset view needs **Meta Quest Link** (or Device Simulator on desktop).

## One Editor only

- Project: `C:\Users\KING\projects\QHYSICS`
- Branch: `reality-engine`
- Unity: **6000.7.0a4** (one instance ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â never a second Editor on this project)

## Who is the player?

**XR Origin IS the player** (CharacterController + Camera Offset + Main Camera + XRI hands).

On **desktop** (Editor Ctrl+P, no Quest Link / no running XR display):
- A simple **DesktopBody** (torso + head + hand proxies) stands under XR Origin - not parented to Main Camera / never under Giza pyramids.
- Head uses layer 31 so your own camera does not draw it (first-person).
- Hand proxies sit in front of the view; **E / LMB grab** attaches held props to the right-hand attach point.
- Bottom **hotbar 1-9** is the desktop inventory (VR toolbelt still works via M / Tab).

On **Quest Link**: desktop body hides; XR locomotion + Left/Right Hand Direct/Ray interactors take over. Light `XrControllerProxy` grips show on each controller (Hand Presence still used when OpenXR devices match). Grab attach is on each hand's `Attach` Ã¢â‚¬â€ desktop HandAttach does not fight XR grabs. If the headset shows nothing, fix Link Ã¢â‚¬â€ you are not missing a character mesh.


## Play steps ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Meta Quest Link (Quest 3S)

1. On PC: install/open **Meta Quest Link** (Air Link or cable). Quest in Developer Mode, same account, PC allowed.
2. Put on headset ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ enable **Link** / connect to this PC. Confirm Link status is Connected.
3. In Unity (only one Editor): open `Assets/Scenes/Faraday.unity`.
4. Optional once: **Reality Engine ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Fix Player Spawn** (or **Reset Player at Lab**) then **Ctrl+S** ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â parks XR Origin on LabPlaza north of the circuit table facing Khufu, enables Main Camera, wires locomotion XR Origin.
5. Press **Ctrl+P** (Play). Game view mirrors the HMD via OpenXR + Link.
6. First run: world **Main Menu ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Enter Sandbox** (or skip if already entered).
7. Interact: teleport / smooth move on plaza, grab circuit parts, Toolbelt **M / Tab / Menu**, Pause **Esc / P**.

If Play works in Editor Game view but the headset stays on the Quest home / black: Link is not active ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â fix Link first. Do not build APK for daily testing.


## Play steps ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â XR Device Simulator (desktop, no headset)

1. Package Manager ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ **XR Interaction Toolkit** ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Samples ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ import **XR Device Simulator** (once).
2. Project Settings ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ **XR Plug-in Management** ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ **XR Interaction Toolkit** ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ enable **Use XR Device Simulator in scenes** / auto-instantiate (or add the `XR Device Simulator` prefab to Faraday).
   - Asset: `Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â set simulator prefab after sample import; `Automatically Instantiate Simulator Prefab` can be on for Editor-only.
3. Open Faraday ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ **Ctrl+P**. Use keyboard/mouse per simulator HUD (move/look/grip).
4. Optional: **Reality Engine ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Fix Player Spawn** before Play if Origin pose looks wrong in Hierarchy.


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
| **LMB** / **Scroll** while holding CIRCUIT gadget (incl. Motor/Solar/Capacitor/Inductor/Diode/Fuse/Function Generator), Multimeter, Galvanometer, Oscilloscope, Frequency Counter, Power Meter, Probe, Stopwatch, or Field Lens |

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
| **M** / **Tab** | Toolbelt (worn at hip/chest with lag Ã¢â‚¬â€ not glued to camera) |

When a Quest Link headset is connected / XR display running, desktop locomotion disables and XR continuous-move + snap turn own the CharacterController (no fight with WASD). Toolbelt follows HipAnchor on DesktopBody / XR Origin. Enter Sandbox / Fix Player Spawn recalibrates Floor + eye height.

### CIRCUIT Motor (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD â†’ Motor**; grab it.
3. Hold near Coil; rotor spins from classical |I| (pair with Battery and/or Magnet sweep + Resistor).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles gear 0.5x / 1x / 2x / REV. Honesty: kinematic spin proxy, not torque/back-EMF.

### CIRCUIT Solar (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Solar**; grab it.
3. Hold near Coil; classical series EMF from irradiance preset (pair with Lamp/Motor/Resistor/Multimeter).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles OFF / Dawn / Noon / Bright (0 / 1.5 / 3 / 6 V). Honesty: lumped photocurrent/irradiance EMF proxy — NOT a real PV I-V curve, not quantum, not MPPT.

### CIRCUIT Capacitor (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Capacitor**; grab it.
3. Hold near Coil; classical series C (pair with Battery + Resistor/Lamp; Multimeter I falls as Vc rises).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles SHORT / 1mF / 10mF / 100mF. Honesty: lumped RC only — NOT dielectric physics, not ESR/ESL. **New Run** clears Vc.

### CIRCUIT Inductor (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Inductor**; grab it.
3. Hold near Coil; classical series L (pair with Battery + Resistor/Lamp; Multimeter I ramps; with Capacitor = RLC).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles SHORT / 1mH / 10mH / 100mH. Honesty: lumped RL/RLC only — NOT core saturation, not skin effect, not mutual M. **New Run** clears I_L (and Vc).

### CIRCUIT Diode (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Diode**; grab it.
3. Hold near Coil; ideal series diode clamp (pair with Magnet sweep + Multimeter **I** / Lamp; I one-sided).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles SHORT / FWD / REV. Honesty: ideal half-wave only — NOT Shockley equation, not recovery, not avalanche; inductive kick not snubbered.

### CIRCUIT Fuse (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Fuse**; grab it.
3. Hold near Coil; ideal |I| trip open (pair with Magnet sweep + Multimeter **I** / Lamp; hard sweep blows fuse and I collapses).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles 5mA / 20mA / 50mA / BYPASS. While **BLOWN**, activate / **N** rearms. Honesty: ideal |I| threshold only — NOT I2t, not arc, not thermal model.

### CIRCUIT LED (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> LED**; grab it.
3. Hold near Coil; ideal series diode + colored glow (pair with Magnet sweep or Battery + Resistor; Multimeter **I** one-sided on color presets).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles RED / GREEN / BLUE / SHORT. Honesty: ideal diode + emission proxy � NOT bandgap photons, not real LED I-V, not thermal.

### CIRCUIT Speaker (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Speaker**; grab it.
3. Hold near Coil; hear classical |I| as a procedural sine (pair with Magnet sweep or Battery + Resistor; Multimeter **I**).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles MUTE / LO / MID / HI gain. Honesty: sine |I| proxy - NOT a real voice coil, not Lorentz force audio, not AC spectrum.

### CIRCUIT Potentiometer (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Potentiometer**; grab it.
3. Hold near Coil; applies classical series R_load via discrete wiper presets (pair with Battery + Lamp/Motor/Multimeter).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles 1Ω / 5Ω / 10Ω / 25Ω / 100Ω / 1kΩ (default 10Ω). Honesty: discrete lumped R_load wiper steps — NOT a real potentiometer, not 3-terminal divider, not taper curve. Overrides Resistor when both near; Switch OPEN still wins.

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
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles EMF0.5 / EMF1 / EMF2 / I0.5 / I1 / I2 / Phi1 / Phi2 (default EMF1 = EMF 1s). Honesty: ideal rolling strip from classical InductionCircuit Emf/I/Phi samples — NOT real ADC, not triggered scope, not FFT, not probe capacitance.





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

### CIRCUIT Function Generator (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Function Generator**; grab it.
3. Hold near Coil; drives classical ideal series EMF waveform (pair with MEASURE Oscilloscope / Frequency Counter / Power Meter / Multimeter / Galvanometer / Lamp).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles OFF / SIN1 / SIN5 / SIN10 / SQR5 / SQR10 / TRI5 / TRI10 (default SIN5 = 5 Hz @ 1.5 Vpk). Honesty: ideal AWG series EMF on InductionCoil - NOT real DDS, not output impedance, not coil frequency response, not sync/trigger.
## Play steps ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â checklist

1. Open `Assets/Scenes/Faraday.unity` (Build Settings already has Faraday enabled).
2. Confirm Hierarchy has **LabLandscape** (Giza) and **QhysicsUI** (or RealityEngine host). If missing:
   - Menu **Reality Engine ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Place Giza Complex**
   - Menu **Reality Engine ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Place QHYSICS UI**
   - **Ctrl+S** / File ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Save so placement persists.
3. Optional: **Reality Engine ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Fix Player Spawn** (XR Origin on plaza, Main Camera MainCamera+enabled, facing Khufu).
4. Press **Ctrl+P** (or the Play button).
5. Headset: Quest Link / OpenXR, **or** Editor with **XR Device Simulator**.
6. First run: world **Main Menu ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Enter Sandbox** (or skip if already entered). Short onboarding tips are dismissible (Next / Skip).
7. Interact:
   - Move with XRI locomotion / teleport on plaza
   - Grab circuit parts from table dispensers (grip)
   - Build Battery ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Wire ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Bulb ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Switch loop
   - **M / Tab / Menu / B / Grip** toggles Toolbelt
   - **Esc / P** opens Pause ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Resume / Reset Experiment / Settings / Exit Play (Editor)
   - SimChip (`> 1x`) cycles sim speed / pause / reset (Reset also calls CircuitLab.Reset)

## Shipping only

- **Ctrl+B** / Build and Run ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Android (Quest) or Windows player.
- Quest: Developer Mode + authorized `adb devices` before APK install.
- Do **not** build APK unless `adb devices` shows the authorized Quest serial you intend to flash.

## Never

- Second Unity on this project
- `GetInstanceID` in tooling paths
- `Sprites/Default` for Giza/lab surfaces
- Disabling MountainScene
- Assuming Ctrl+P installs to the headset (it does not)

