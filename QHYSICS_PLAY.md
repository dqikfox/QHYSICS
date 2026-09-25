# QHYSICS â€” how to Play (Editor)

## UI map (lab shell)

Runtime auto-ensures on Play via `QhysicsUiBootstrap` (no scene YAML rewrite required).
Editor: **Reality Engine -> Place QHYSICS UI** or **Reality Engine -> QHYSICS -> Ensure UI**.

| Piece | What | Toggle / notes |
|-------|------|----------------|
| **Toolbelt** (primary) | Hip/chest world panel â€” BUILD / PHYSICS / MEASURE / WORLD / EXPERIMENTS | **M / Tab / Menu / B / Grip**. MEASURE pages with scroll or `< >`. |
| **Status HUD** | QHYSICS + experiment name + run dot | Follows non-dominant side / camera |
| **Inspect** | Hover = summary; hold = detail (EMF/I/Phi, mass, etc.) | Ray / desktop hover / grab |
| **Sim chip** | Pause / 0.25x / 1x / 2x + **New Run** (clears `Gadget_*`) | Expand chip near right forearm |
| **Pause** | Resume / Reset Experiment / Settings / Exit Play (Editor) | **Esc / P** â€” lab pause, not arcade |
| **Onboarding strip** | One line: BOOT -> ENTER -> INTERACT -> EXPERIMENT | Dismiss **X** (PlayerPrefs) |
| **Main menu** | Enter Sandbox (first run) | Auto-hides after enter |

Visual language: dark glass panels, cyan `#00E5FF` accents, TMP LiberationSans, URP-safe `UI/Default` (never Sprites/Default).
XR: EventSystem + `XRUIInputModule` + `TrackedDeviceGraphicRaycaster`; Event Camera = XR / Main Camera.

### Ctrl+P checklist

1. Open `Assets/Scenes/Faraday.unity` (one Editor only).
2. Optional: **Reality Engine -> QHYSICS -> Ensure UI** (or trust runtime bootstrap).
3. Optional: **Fix Player Spawn** / **Ensure Player Character**.
4. **Ctrl+P**. Desktop WASD works without headset; Quest Link or XR Device Simulator for XR.
5. Enter Sandbox if shown -> dismiss onboarding strip -> **O** operator (optional) -> **M** toolbelt -> hover a gadget for Inspect -> Sim chip for speed -> **Esc** pause (Operator Select available).
6. Training: pick up plaza kits with **E**, equip baton with **U**, **LMB** training drones east of plaza.
6. EXPERIMENTS -> **New Run** clears spawned gadgets (Giza/lab content stays).

---

## One Editor only

- Project: `C:\Users\KING\projects\QHYSICS`
- Branch: `reality-engine`
- Unity: **6000.7.0a4** (one instance ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â never a second Editor on this project)



## Operator / Carry / Training System (player vertical slice)

Runtime auto-ensures via `QhysicsPlayerSystemsBootstrap` (also from `QhysicsUiBootstrap` / menu **Reality Engine -> QHYSICS -> Ensure Player Systems**). Lab training flavour â€” not fantasy MMO combat.

### Operator select (3 archetypes)

| Operator | Kit bias | Move | Sprint | HP | Slots |
|----------|----------|------|--------|----|-------|
| **Field Scientist** | Probe tip, battery, ampoule, baton | 3.4 | x2.0 | 100 | 6 |
| **Lab Engineer** | Extra batteries + shield cell | 3.2 | x1.85 | 110 | 8 |
| **Survey Ranger** | Dual batons, mobility | 3.9 | x2.35 | 90 | 5 |

- **O** â€” Operator Select panel (dark glass + cyan). Also **Esc/P -> Operator Select**.
- Choice applies locomotion, max HP, carry slots, starting kit, body/arm tint.
- Persisted in `PlayerPrefs` key `qhysics.operator.id`.

### Carry inventory (alongside BUILD hotbar 1â€“9)

World pickups implement `IQhysicsInteractable` -> **E** / LMB interact adds to carry slots (hip-pouch story; BUILD hotbar unchanged). **XR:** grip near plaza kits picks up into carry (trigger still baton swing).

| Key | Action |
|-----|--------|
| **[** / **]** | Select carry slot |
| **U** | Use consumable (ampoule / shield cell) or equip Training Baton |
| **X** | Drop selected carry item into world |
| **XR stick L/R** | Cycle carry slots |
| **XR stick click** | Use / equip selected (same as **U**) |
| **XR stick down** | Drop selected (same as **X**) |
| **E** | Pick up highlighted world item (Health Ampoule, Battery Pack, Probe Tip, Training Baton, Shield Cell) |

Carry strip HUD sits above the BUILD hotbar; vitals chip top-left labelled **TRAINING SYSTEM**.

### Training combat

- Equip **Training Baton** (**U** on baton slot). Soft damage to training drones (same cone for desktop + XR).
- **Desktop:** **LMB** / Fire1 swings while baton equipped.
- **XR (Quest Link):** controller **trigger** (activate) on either hand swings; aim uses that controller pose (falls back to HMD forward).
- Drones float east of plaza, fire soft pulses; brighter hit flash + floating damage chips; player HP chip flashes red on hurt.
- Drone HP to 0 soft-disables (dim body + RESPAWN countdown chip) then restores after ~6s ï¿½ GameObject stays active so the timer runs (Invoke would die on SetActive false).
- Operator HP regen after delay; ampoule heals; shield cell softens damage briefly.
- HP -> 0 respawns at plaza via `LabPlayerSpawn` (no softlock / no game-over).

### XR UI (headset)

- **Operator Select** â€” world-space panel (Esc/P -> Operator Select, or **O** on companion keyboard). Event Camera = XR Main Camera (`WireEventCamera`).
- **Carry / Training HUD** â€” promotes to world-space follow when an XR display is running (left of view); stays ScreenSpaceOverlay on desktop. Never hidden in headset.

### Spawn locations (Ctrl+P)

Under runtime root `QhysicsTrainingWorld` (relative to XR Origin / LabPlaza):

- Pickups ~1â€“2 m around plaza stand pose (ampoule, battery, probe tip, baton, shield cell).
- Training drones ~3.5â€“4.2 m east of plaza at ~1.4â€“1.6 m height.

### Ctrl+P quick check

1. Faraday -> Ctrl+P -> Enter Sandbox.
2. **O** -> pick Survey Ranger (or Field Scientist) -> Close.
3. Walk to cyan-highlighted pickups near plaza -> **E** to bag.
4. **]** to baton -> **U** equip -> **LMB** hit a Training Drone.
5. Take a pulse hit -> HP chip updates -> **U** ampoule to heal.
6. **Esc -> Operator Select** to switch archetype (kit refill).



### XR combat Ctrl+P check (Quest Link)

1. Link connected -> Faraday -> Ctrl+P -> Enter Sandbox.
2. **O** or Pause -> Operator Select (world panel, laser/ray clickable) -> pick Survey Ranger.
3. Point controller at plaza pickups -> **grip** to bag (companion **E** still works).
4. Stick L/R select baton slot + **stick-click** equip (or companion **U**) ï¿½ aim at Training Drone ï¿½ **trigger** to swing.
5. Take a drone pulse ï¿½ TRAINING SYSTEM HP chip flashes ï¿½ stick-click ampoule (or **U**) to heal; stick-down drops selected.
6. Confirm Carry HUD floats in world-space (not missing in HMD).

## Who is the player?

**XR Origin IS the player** (CharacterController + Camera Offset + Main Camera + XRI hands).

On **desktop** (Editor Ctrl+P, no Quest Link / no running XR display):
- A simple **DesktopBody** (torso + head + hand proxies) stands under XR Origin - not parented to Main Camera / never under Giza pyramids.
- Head uses layer 31 so your own camera does not draw it (first-person).
- Hand proxies sit in front of the view; **E / LMB grab** attaches held props to the right-hand attach point.
- Bottom **hotbar 1-9** is the desktop inventory (VR toolbelt still works via M / Tab).

On **Quest Link**: desktop body hides; XR locomotion + Left/Right Hand Direct/Ray interactors take over. Light `XrControllerProxy` grips show on each controller (Hand Presence still used when OpenXR devices match). Grab attach is on each hand's `Attach` ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â desktop HandAttach does not fight XR grabs. If the headset shows nothing, fix Link ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â you are not missing a character mesh.


## Play steps ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â Meta Quest Link (Quest 3S)

1. On PC: install/open **Meta Quest Link** (Air Link or cable). Quest in Developer Mode, same account, PC allowed.
2. Put on headset ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ enable **Link** / connect to this PC. Confirm Link status is Connected.
3. In Unity (only one Editor): open `Assets/Scenes/Faraday.unity`.
4. Optional once: **Reality Engine ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Fix Player Spawn** (or **Reset Player at Lab**) then **Ctrl+S** ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â parks XR Origin on LabPlaza north of the circuit table facing Khufu, enables Main Camera, wires locomotion XR Origin.
5. Press **Ctrl+P** (Play). Game view mirrors the HMD via OpenXR + Link.
6. First run: world **Main Menu ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Enter Sandbox** (or skip if already entered).
7. Interact: teleport / smooth move on plaza, grab circuit parts, Toolbelt **M / Tab / Menu**, Pause **Esc / P**.

If Play works in Editor Game view but the headset stays on the Quest home / black: Link is not active ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â fix Link first. Do not build APK for daily testing.


## Play steps ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â XR Device Simulator (desktop, no headset)

1. Package Manager ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ **XR Interaction Toolkit** ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Samples ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ import **XR Device Simulator** (once).
2. Project Settings ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ **XR Plug-in Management** ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ **XR Interaction Toolkit** ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ enable **Use XR Device Simulator in scenes** / auto-instantiate (or add the `XR Device Simulator` prefab to Faraday).
   - Asset: `Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â set simulator prefab after sample import; `Automatically Instantiate Simulator Prefab` can be on for Editor-only.
3. Open Faraday ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ **Ctrl+P**. Use keyboard/mouse per simulator HUD (move/look/grip).
4. Optional: **Reality Engine ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Fix Player Spawn** before Play if Origin pose looks wrong in Hierarchy.


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
| **Ctrl** | Crouch (CC height + Camera Offset eye duck ~0.95m) |
| **H** | Recalibrate eye height (~1.65m Camera Offset / Floor) |
| **1-9** | Hotbar: Wire, Battery, Switch, Bulb, Resistor, Magnet, Field Lens, Cubit Rod, Delete |
| **Q** / **Scroll** | Cycle hotbar |
| **LMB** | Spawn selected part (or Delete when slot 8) |
| **E** / **LMB** on part | Grab |
| **LMB** / **Scroll** while holding CIRCUIT gadget (incl. Motor/Solar/Capacitor/Inductor/Diode/Fuse/Function Generator), Multimeter, Galvanometer, Oscilloscope, Frequency Counter, Power Meter, Flux Meter, Charge Meter, Voltmeter, Ammeter, Ohmmeter, Capacitance Meter, Inductance Meter, Resonance Meter, Impedance Meter, Power Factor Meter, Q Factor Meter, Admittance Meter, Decibel Meter, Crest Factor Meter, Energy Meter, Duty Cycle Meter, Slew Rate Meter, Rise/Fall Meter, Overshoot Meter, Peak-to-Peak Meter, Mean Meter, Ripple Meter, THD Meter, Probe, Compass, Stopwatch, or Field Lens |

### MEASURE Multimeter (CircuitLab fallback)
When no InductionCircuit is bound, Multimeter also reads the nearest placed CircuitLab component V/I (`GetVoltage` / `GetCurrentValue`) with honesty tag `[CircuitLab component]`. InductionCircuit still wins when present.
### CIRCUIT Resistor (hotbar 5)
1. Enter Sandbox / Induction.
2. Hotbar **5** (or toolbelt) spawn **Resistor**; grab it.
3. Hold near Coil; **N/P**, VR trigger, or desktop **LMB/scroll** cycles 2 / 8 / 50 / OPEN Ohm classical R_load.
4. Pair with Magnet sweep + Multimeter **I** / Lamp glow.
 Activate / cycle (desktop trigger; Multimeter pages; Probe NEAREST/ALL/COMP; Compass ALL/NEAREST/LAB; Stopwatch LMB toggle / scroll-reset; Field Lens layer peel) |
| **F** / **RMB** | Drop |
| **R** | Throw |
| **Esc** / **P** | Pause (releases cursor) - Resume re-locks |
| **M** / **Tab** | Toolbelt (hip/chest lag follow; overflow tabs: scroll or < > pages) |

When a Quest Link headset is connected / XR display running, desktop locomotion disables and XR continuous-move + snap turn own the CharacterController (no fight with WASD). Toolbelt follows HipAnchor on DesktopBody / XR Origin. Enter Sandbox / Fix Player Spawn recalibrates Floor + eye height.

### CIRCUIT Motor (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Motor**; grab it.
3. Hold near Coil; rotor spins from classical |I| (pair with Battery and/or Magnet sweep + Resistor).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles gear 0.5x / 1x / 2x / REV. Honesty: kinematic spin proxy, not torque/back-EMF.

### CIRCUIT Solar (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Solar**; grab it.
3. Hold near Coil; classical series EMF from irradiance preset (pair with Lamp/Motor/Resistor/Multimeter).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles OFF / Dawn / Noon / Bright (0 / 1.5 / 3 / 6 V). Honesty: lumped photocurrent/irradiance EMF proxy Ã¢â‚¬â€ NOT a real PV I-V curve, not quantum, not MPPT.

### CIRCUIT Capacitor (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Capacitor**; grab it.
3. Hold near Coil; classical series C (pair with Battery + Resistor/Lamp; Multimeter I falls as Vc rises).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles SHORT / 1mF / 10mF / 100mF. Honesty: lumped RC only Ã¢â‚¬â€ NOT dielectric physics, not ESR/ESL. **New Run** clears Vc.

### CIRCUIT Inductor (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Inductor**; grab it.
3. Hold near Coil; classical series L (pair with Battery + Resistor/Lamp; Multimeter I ramps; with Capacitor = RLC).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles SHORT / 1mH / 10mH / 100mH. Honesty: lumped RL/RLC only Ã¢â‚¬â€ NOT core saturation, not skin effect, not mutual M. **New Run** clears I_L (and Vc).

### CIRCUIT Diode (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Diode**; grab it.
3. Hold near Coil; ideal series diode clamp (pair with Magnet sweep + Multimeter **I** / Lamp; I one-sided).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles SHORT / FWD / REV. Honesty: ideal half-wave only Ã¢â‚¬â€ NOT Shockley equation, not recovery, not avalanche; inductive kick not snubbered.

### CIRCUIT Fuse (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Fuse**; grab it.
3. Hold near Coil; ideal |I| trip open (pair with Magnet sweep + Multimeter **I** / Lamp; hard sweep blows fuse and I collapses).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles 5mA / 20mA / 50mA / BYPASS. While **BLOWN**, activate / **N** rearms. Honesty: ideal |I| threshold only Ã¢â‚¬â€ NOT I2t, not arc, not thermal model.

### CIRCUIT LED (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> LED**; grab it.
3. Hold near Coil; ideal series diode + colored glow (pair with Magnet sweep or Battery + Resistor; Multimeter **I** one-sided on color presets).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles RED / GREEN / BLUE / SHORT. Honesty: ideal diode + emission proxy Ã¯Â¿Â½ NOT bandgap photons, not real LED I-V, not thermal.

### CIRCUIT Speaker (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Speaker**; grab it.
3. Hold near Coil; hear classical |I| as a procedural sine (pair with Magnet sweep or Battery + Resistor; Multimeter **I**).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles MUTE / LO / MID / HI gain. Honesty: sine |I| proxy - NOT a real voice coil, not Lorentz force audio, not AC spectrum.

### CIRCUIT Potentiometer (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Potentiometer**; grab it.
3. Hold near Coil; applies classical series R_load via discrete wiper presets (pair with Battery + Lamp/Motor/Multimeter).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles 1ÃŽÂ© / 5ÃŽÂ© / 10ÃŽÂ© / 25ÃŽÂ© / 100ÃŽÂ© / 1kÃŽÂ© (default 10ÃŽÂ©). Honesty: discrete lumped R_load wiper steps Ã¢â‚¬â€ NOT a real potentiometer, not 3-terminal divider, not taper curve. Overrides Resistor when both near; Switch OPEN still wins.

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
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles EMF0.5 / EMF1 / EMF2 / I0.5 / I1 / I2 / Phi1 / Phi2 (default EMF1 = EMF 1s). Honesty: ideal rolling strip from classical InductionCircuit Emf/I/Phi samples Ã¢â‚¬â€ NOT real ADC, not triggered scope, not FFT, not probe capacitance.





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
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles PHI / DPHI / PEAK / CLR (default PHI; CLR zeros peak then snaps to PHI). Honesty: ideal lumped fluxmeter from InductionCircuit - NOT real integrating fluxmeter, not search-coil ballistic galvo, not Hall BÃ‚Â·A, not hysteresis tracer.

### MEASURE Charge Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Charge Meter**; grab it.
3. Hold near Coil; classical Q=Ã¢Ë†Â«I dt (C), peak |Q|, or Iavg (=Q/t since CLR) (pair with BUILD Capacitor / Function Generator / Magnet; Multimeter I / Power Meter for cross-check).
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
3. Hold near Coil; classical energy integrator E=âˆ«P dt from LoadPowerWatts, plus live P and window AVG=E/t (pair with BUILD Function Generator / Battery; Power Meter / Decibel Meter for cross-check). Energy accumulates while linked and within tip range.
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

### MEASURE Rise/Fall Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Rise/Fall Meter**; grab it.
3. Hold near Coil; classical 10%-90% Emf rise (RISE), 90%-10% fall (FALL), and live in-progress edge (LIVE) (pair with BUILD Function Generator SQR/TRI edges; Oscilloscope / Slew Rate Meter / Duty Cycle Meter for cross-check). Sharp square edges should show short RISE/FALL.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles RISE / FALL / LIVE / CLR (default RISE; CLR zeros times and the edge state machine, then snaps to RISE). Honesty: ideal threshold timing from classical InductionCircuit Emf samples - NOT a real rise-time meter, not scope cursors, not BER/edge analyzer, not calibrated pulse-edge hardware.

### MEASURE Overshoot Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Overshoot Meter**; grab it.
3. Hold near Coil; classical Emf overshoot % (OSV), I overshoot % (OSI), and peak |Emf| (PK) (pair with BUILD Function Generator SQR edges; Oscilloscope / Rise/Fall Meter / Slew Rate Meter for cross-check). Ringing or soft edges after a step should raise OSV.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles OSV / OSI / PK / CLR (default OSV; CLR zeros peaks/windows and the edge state machine, then snaps to OSV). Honesty: ideal overshoot % from classical InductionCircuit Emf/I samples - NOT a real overshoot meter, not scope cursors, not BER/edge analyzer, not calibrated pulse-edge / step-response hardware.

### MEASURE Peak-to-Peak Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Peak-to-Peak Meter**; grab it.
3. Hold near Coil; classical Emf Vpp (PPV), I Ipp (PPI), and live Emf window span (LIVE) (pair with BUILD Function Generator; Oscilloscope / Crest Factor Meter / Overshoot Meter for cross-check). Larger swing should raise PPV.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles PPV / PPI / LIVE / CLR (default PPV; CLR zeros soft-windows and held peaks, then snaps to PPV). Honesty: ideal peak-to-peak from classical InductionCircuit Emf/I samples - NOT a real Vpp meter, not scope cursors, not true-RMS ADC, not calibrated waveform analyzer.

### MEASURE Mean Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Mean Meter**; grab it.
3. Hold near Coil; classical windowed mean Emf (DCV), mean I (DCI), and mean |Emf| (MAV) (pair with BUILD Function Generator; Oscilloscope / Peak-to-Peak Meter / Crest Factor Meter for cross-check). A biased swing should shift DCV away from zero.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles DCV / DCI / MAV / CLR (default DCV; CLR zeros accumulators, then snaps to DCV). Honesty: ideal windowed mean Emf/I from classical InductionCircuit samples - NOT real DMM DC mode, not integrating ADC, not true-RMS, not calibrated offset meter.

### MEASURE Ripple Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Ripple Meter**; grab it.
3. Hold near Coil; classical Vac/|Vdc| (RPV), Iac/|Idc| (RPI), and AC Emf Vac (VAC) (pair with BUILD Function Generator; Mean Meter / Peak-to-Peak Meter / Crest Factor Meter for cross-check). A pure AC swing with near-zero DCV should drive high RPV; a DC offset lowers it.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles RPV / RPI / VAC / CLR (default RPV; CLR zeros accumulators, then snaps to RPV). Honesty: ideal windowed AC residual Vac=sqrt(max(0,Vrms^2-Vdc^2)) from classical InductionCircuit samples - NOT real ripple meter, not AC-coupled DMM, not scope math, not calibrated ripple hardware.

### MEASURE THD Meter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> THD Meter**; grab it.
3. Hold near Coil; classical windowed harmonic residual THD for Emf (THDV), I (THDI), and sine-assumed fund Emf (FUND) (pair with BUILD Function Generator; Ripple Meter / Crest Factor Meter / Mean Meter for cross-check). A pure sine should drive low THDV; a square/triangle raises it.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles THDV / THDI / FUND / CLR (default THDV; CLR zeros window, then snaps to THDV). Honesty: ideal windowed harmonic residual from classical InductionCircuit Emf/I samples - NOT real THD analyzer, not FFT spectrum analyzer, not IEC distortion meter, not calibrated audio THD hardware.

### MEASURE Magnetic Compass (toolbelt MEASURE)
1. Enter Sandbox / Induction (or PHYSICS Magnet).
2. Toolbelt **MEASURE -> Compass** (or spawn label Compass); grab the disc.
3. Needle aligns to horizontal B from lab magnets + weak ambient lab north (world +Z, ~50 uT). Walk near Induction magnet / Handheld Magnet: needle swings; far away: rests toward lab north.
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles ALL / NEAREST / LAB (default ALL = dipoles + Earth; LAB = dipoles only). Honesty: classical MagneticDipole B + ambient Earth field - NOT a fluxgate, not a gyroscope.
5. Ctrl+P test: WASD near magnet; M/Tab toolbelt -> MEASURE -> Compass; watch |Bh| / heading readout.

### Hand Crank Generator (PHYSICS)
1. Ctrl+P Play -> Enter Sandbox -> **M/Tab** toolbelt -> **PHYSICS** -> **Crank Generator** (or spawn key `crank` / `generator` / `dynamo`).
2. Place near Induction Lab coil; grab; **hold activate / LMB** to crank (release coasts). **N/P** or scroll cycles SLOW / MED / FAST / TURBO RPM.
3. Watch coil EMF / Flux Meter / Galvanometer as the rotating MagneticDipole sweeps flux (Faraday). Honesty: rotating dipole near coil - NOT a commutated dynamo / brushes / commercial alternator.
4. Desktop WASD; C Challenges; M/Tab toolbelt.

### Mutual Coupler (PHYSICS)
1. Ctrl+P Play -> Enter Sandbox -> **M/Tab** toolbelt -> **PHYSICS** -> **Mutual Coupler** (or spawn key `mutual` / `coupler` / `mutual inductance`).
2. Place near the Induction Lab coil (closer coil = primary). If only one coil is in range, a buddy secondary coil is created on the gadget.
3. Drive primary current: thrust the lab magnet, use **Crank Generator**, or **BUILD Function Generator** on the primary. Grab the coupler; **N/P** / VR trigger / LMB-scroll cycles OFF / WEAK (0.5 mH) / MED (2 mH) / STRONG (10 mH).
4. Readout shows Ip, Es=-M dIp/dt, peak |Es|. Pair with MEASURE Voltmeter / Galvanometer / Oscilloscope on the secondary. Honesty: lumped mutual M only — NOT geometric flux linkage, not core hysteresis, not leakage, not dual-winding transformer (BUILD Transformer is turns-tap).
5. Desktop WASD; C Challenges; M/Tab toolbelt.

### Spring-Mass (PHYSICS)
1. Ctrl+P Play -> Enter Sandbox -> **M/Tab** toolbelt -> **PHYSICS** -> **Spring Mass** (or spawn key `spring` / `mass` / `hooke`).
2. Gadget spawns with a slight pluck and oscillates (lumped Hooke: m x'' = -k x - c x', m=0.25 kg). Grab it; **N/P** / VR trigger / LMB-scroll cycles k: SOFT / MED / STIFF / RIGID. **Shift+N/P** cycles damp: OFF / LIGHT / HEAVY.
3. Readout shows displacement x, force F=-kx, spring energy 0.5 k x^2, natural frequency f0. Honesty: lumped Hooke spring-mass — NOT continuum elasticity, not nonlinear, not collision contact spring.
4. Desktop WASD; C Challenges; M/Tab toolbelt.

### Simple Pendulum (PHYSICS)
1. Ctrl+P Play -> Enter Sandbox -> **M/Tab** toolbelt -> **PHYSICS** -> **Pendulum** (or spawn key `pendulum` / `pend` / `swing` / `bob`).
2. Gadget spawns with a ~25 deg pluck and swings (lumped planar: theta'' = -(g/L) sin(theta) - c theta'', m=0.25 kg, g=9.81). Grab it; **N/P** / VR trigger / LMB-scroll cycles L: SHORT (0.15 m) / MED (0.25 m) / LONG (0.40 m) / XL (0.60 m). **Shift+N/P** cycles damp: OFF / LIGHT / HEAVY.
3. Readout shows theta (deg), omega, small-angle period T~2pi sqrt(L/g), PE = mgL(1-cos theta), KE = 0.5 m (L omega)^2. Honesty: lumped planar pendulum - NOT spherical pendulum, not rigid-body collision, not air drag Cd model, not physical string stretch.
4. Desktop WASD; C Challenges; M/Tab toolbelt.

### Thin Lens / Optics Bench (PHYSICS)
1. Ctrl+P Play -> Enter Sandbox -> **M/Tab** toolbelt -> **PHYSICS** -> **Thin Lens** (or spawn key `lens` / `optics` / `thinlens` / `focus`).
2. Gadget spawns an optics bench with URP Lit disc lens, amber object marker, and ghost image marker (Gaussian thin lens: 1/f = 1/u + 1/v, m = -v/u). Grab it; **N/P** / VR trigger / LMB-scroll cycles f: CONVEX5 (5 cm) / CONVEX10 (10 cm) / CONVEX20 (20 cm) / CONCAVE (-10 cm). **Shift+N/P** cycles object distance u presets (8/12/15/25/40 cm).
3. Readout shows f, u, v, m, and REAL / VIRTUAL / NO IMAGE. Image marker scales by |m| and inverts when m < 0. Honesty: ideal thin lens, paraxial — NOT thick lens, not chromatic/spherical aberration, not wave optics.
4. Desktop WASD; C Challenges; M/Tab toolbelt.


### Atwood Machine (PHYSICS)
1. Ctrl+P Play -> Enter Sandbox -> **M/Tab** toolbelt -> **PHYSICS** -> **Atwood** (or spawn key `atwood` / `pulley` / `twomass` / `two-mass`).
2. Gadget spawns a stand + overhead pulley with two hanging masses (ideal Atwood: a = g(m1-m2)/(m1+m2), T = 2 m1 m2 g/(m1+m2), g=9.81). Grab it; **N/P** / VR trigger / LMB-scroll cycles mass pairs: EQ (0.20/0.20 kg) / LIGHT (0.25/0.15) / MED (0.30/0.15) / HEAVY (0.40/0.10). **Shift+N/P** cycles damp: OFF / LIGHT / HEAVY.
3. Readout shows preset, a (m/s^2), T (N), v, and dh. Soft end-stops keep masses from passing the pulley or floor. Honesty: lumped ideal Atwood — NOT real pulley inertia, not string mass, not friction, not air drag, not 3D swinging.
4. Desktop WASD; C Challenges; M/Tab toolbelt.
### Inclined Plane (PHYSICS)
1. Ctrl+P Play -> Enter Sandbox -> **M/Tab** toolbelt -> **PHYSICS** -> **Inclined Plane** (or spawn key `incline` / `inclined` / `inclinedplane` / `ramp` / `plane` / `wedge`).
2. Gadget spawns a ramp plank + sliding block (ideal inclined plane: a = g(sin θ − μ cos θ), g=9.81). Grab it; **N/P** / VR trigger / LMB-scroll cycles angle: LO (15°) / MED (30°) / STEEP (45°) / CLIFF (60°). **Shift+N/P** cycles friction μ: OFF (0) / LIGHT (0.2) / HEAVY (0.4). When μ cos θ ≥ sin θ the block shows STICK (a=0).
3. Readout shows θ, μ, a (m/s^2 downhill), v, and s (distance along plane). Soft end-stops at top/bottom. Honesty: lumped ideal inclined plane — NOT rolling, not air drag, not variable μ, not 3D tipping.
4. Desktop WASD; C Challenges; M/Tab toolbelt.
### Lever / Torque Balance (PHYSICS)
1. Ctrl+P Play -> Enter Sandbox -> **M/Tab** toolbelt -> **PHYSICS** -> **Lever** (or spawn key `lever` / `torque` / `balance` / `fulcrum` / `seesaw`).
ulcrum / seesaw).
2. Gadget spawns a stand + fulcrum + beam with two point masses (ideal statics: tau_L = -m_L g d_L, tau_R = +m_R g d_R, tau_net = tau_L + tau_R, g=9.81). Grab it; **N/P** / VR trigger / LMB-scroll cycles mass pairs: BAL (1.0/1.0 kg) / LIGHT (1.2/0.8) / MED (1.5/0.5) / HEAVY (2.0/0.5). **Shift+N/P** cycles fulcrum offset: CENTER (arms 0.20/0.20 m) / NEAR_L (0.24/0.12) / FAR_L (0.30/0.10) / NEAR_R (0.12/0.24).
3. Readout shows tau_L, tau_R, tau_net, and BALANCED when |tau_net| < eps (else TIP LEFT / TIP RIGHT). Beam uses light damped tip toward the static tip angle. Honesty: ideal statics tau=mgd, point masses on massless beam - NOT beam flex, not fulcrum friction, not 3D tipping, not distributed beam mass.
4. Desktop WASD; C Challenges; M/Tab toolbelt.
### Parallel-Plate Capacitor / RC (PHYSICS)
1. Ctrl+P Play -> Enter Sandbox -> **M/Tab** toolbelt -> **PHYSICS** -> **Plate Cap** (or spawn key c\ / \platecap\ / \parallel plate\ / \plate capacitor\). Distinct from BUILD **Capacitor** (CIRCUIT series C on InductionCoil).
2. Gadget spawns two URP Lit parallel plates + gap (ideal RC: Q=CV, tau=RC). Grab it; **N/P** cycles C: SMALL (1 uF) / MED (10 uF) / LARGE (100 uF). **Shift+N/P** cycles R: OPEN (hold) / LIGHT (50 kOhm) / MED (100 kOhm) / HEAVY (500 kOhm). VR trigger / LMB-scroll cycles CHARGE target V: 0 / 5 / 12 / 24 V.
3. Readout shows C, R, V, Q=CV (uC), tau=RC, tgt V, and state CHARGING / HOLD / DISCHARGE. With R=OPEN, activate snaps V to target and HOLDs. With R load, V(t) approaches target exponentially (or discharges to 0 when tgt=0V). Cyan plate emission scales with |V|/24. Honesty: ideal lumped RC - NOT dielectric, not ESR/ESL, not fringe, not breakdown, not InductionCircuit C.
4. Desktop WASD; C Challenges; M/Tab toolbelt.

### Projectile Motion / Ballistic Range (PHYSICS)
1. Ctrl+P Play -> Enter Sandbox -> **M/Tab** toolbelt -> **PHYSICS** -> **Projectile** (or spawn key `projectile` / `ballistics` / `ballistic` / `trajectory` / `cannon` / `range` / `throw`).
2. Gadget spawns a barrel + predicted arc markers + ballistic ball (ideal 2D no-drag: R = v^2 sin(2*theta)/g, T = 2*v*sin(theta)/g, H = (v*sin(theta))^2/(2g)). Grab it; **N/P** cycles angle theta: LO (15 deg) / MED (30 deg) / HI (45 deg) / VERT (75 deg). **Shift+N/P** cycles speed v: SLOW (2) / MED (4) / FAST (6) / XFAST (8) m/s. VR trigger / desktop activate **launches** (or resets mid-flight).
3. Readout shows v, theta, R, T, H, and state READY / FLIGHT / LANDED. Arc LineRenderer + sphere markers show the predicted trajectory; the ball animates along it. Honesty: ideal no-drag projectile on flat ground - NOT 3D wind, not Magnus, not bouncing, not rigid-body Unity physics sim of the ball (kinematic visual).
4. Desktop WASD; C Challenges; M/Tab toolbelt.
### MEASURE Voltmeter (toolbelt MEASURE)
1. Enter Sandbox / Induction.
2. Toolbelt **MEASURE -> Voltmeter**; grab it.
3. Hold near Coil; classical Emf (V), peak |Emf|, or Vrms over window since CLR (pair with BUILD Function Generator / PHYSICS Magnet; Multimeter Emf / Oscilloscope EMF for cross-check).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles V / PEAK / RMS / CLR (default V; CLR zeros peak/RMS window then snaps to V). Honesty: ideal EmfVolts from InductionCircuit - NOT real DMM, not true-RMS ADC, not high-Z probe, not differential/isolated meter.
### CIRCUIT Function Generator (toolbelt BUILD)
1. Enter Sandbox / Induction.
2. Toolbelt **BUILD -> Function Generator**; grab it.
3. Hold near Coil; drives classical ideal series EMF waveform (pair with MEASURE Oscilloscope / Frequency Counter / Power Meter / Flux Meter / Charge Meter / Voltmeter / Ammeter / Ohmmeter / Capacitance Meter / Inductance Meter / Resonance Meter / Impedance Meter / Power Factor Meter / Q Factor Meter / Admittance Meter / Decibel Meter / Crest Factor Meter / Energy Meter / Duty Cycle Meter / Slew Rate Meter / Rise/Fall Meter / Overshoot Meter / Peak-to-Peak Meter / Mean Meter / Ripple Meter / THD Meter / Multimeter / Galvanometer / Lamp).
4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles OFF / SIN1 / SIN5 / SIN10 / SQR5 / SQR10 / TRI5 / TRI10 (default SIN5 = 5 Hz @ 1.5 Vpk). Honesty: ideal AWG series EMF on InductionCoil - NOT real DDS, not output impedance, not coil frequency response, not sync/trigger.
## Play steps ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â checklist

1. Open `Assets/Scenes/Faraday.unity` (Build Settings already has Faraday enabled).
2. Confirm Hierarchy has **LabLandscape** (Giza) and **QhysicsUI** (or RealityEngine host). If missing:
   - Menu **Reality Engine ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Place Giza Complex**
   - Menu **Reality Engine ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Place QHYSICS UI**
   - **Ctrl+S** / File ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Save so placement persists.
3. Optional: **Reality Engine ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Fix Player Spawn** (XR Origin on plaza, Main Camera MainCamera+enabled, facing Khufu).
4. Press **Ctrl+P** (or the Play button).
5. Headset: Quest Link / OpenXR, **or** Editor with **XR Device Simulator**.
6. First run: world **Main Menu ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Enter Sandbox** (or skip if already entered). Short onboarding tips are dismissible (Next / Skip).
7. Interact:
   - Move with XRI locomotion / teleport on plaza
   - Grab circuit parts from table dispensers (grip)
   - Build Battery ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Wire ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Bulb ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Switch loop
   - **M / Tab / Menu / B / Grip** toggles Toolbelt
   - **Esc / P** opens Pause ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Resume / Reset Experiment / Settings / Exit Play (Editor)
   - SimChip (`> 1x`) cycles sim speed / pause / reset (Reset also calls CircuitLab.Reset)


## Challenges (CircuitLab campaign)

Runtime systems: `ChallengeManager` + `ChallengeUi` (AutoSpawn). Breadboard resistor dispenser is ensured at Play via `EnsureBreadboardResistorDispenser` (also **Reality Engine ? Ensure Breadboard Resistor Dispenser**).

### Live challenge meters
While a challenge is active, the objective overlay appends live CircuitLab / InductionCircuit readings in brackets (e.g. `[0/1 lit]`, `[R=1 lit=1]`, `[312/120 RPM]`, `[0.012/0.05 V]`) and refreshes about 5x/sec.

### First Light (challenge 1)
1. Ctrl+P ? Enter Sandbox.
2. Press **C** to open the Challenges panel.
3. Start **First Light**.
4. From table dispensers: grab **Battery**, **Wire(s)**, **Bulb**; snap onto breadboard pegs to close a loop.
5. When the bulb lights (significant current), the objective completes and a completion banner shows stars.
6. Completing First Light unlocks **Current Control** (needs the breadboard **Resistor** dispenser).

### Current Control (challenge 2)
1. Press **C** -> Start **Current Control** (unlocked after First Light).
2. Grab a **Resistor** from the auto-spawned Resistor dispenser (beside the Bulb shelf).
3. Place resistor in series with battery + bulb + wires; keep the bulb lit.
4. Both objectives complete -> stars + unlock **Spin Up**. Completion banner shows `Unlocked: Spin Up`.

### Spin Up (challenge 3)
1. Press **C** -> Start **Spin Up** (unlocked after Current Control). Overlay shows live RPM `[0/120 RPM]`.
2. From table dispensers: grab **Battery**, **Wire(s)**, **Motor** (Dispenser2 / Motor shelf); close a loop.
3. When motor RPM >= 120 (live meter), objective completes -> stars + unlock **Double Trouble**.

### Double Trouble (challenge 4)
1. Press **C** -> Start **Double Trouble** (unlocked after Spin Up). Overlay shows live `[0/2 lit]` plus mentor tip.
2. Grab **two Bulbs** from the Bulb dispenser (restocks after each grab), plus **Battery** and **Wire(s)**.
3. Series or parallel both bulbs so both carry significant current; only breadboard-placed clones count.
4. When meter hits `2/2 lit` -> stars + unlock **Sun Power**. Completion banner shows `Unlocked: Sun Power`.


### Sun Power (challenge 5)
1. Press **C** -> Start **Sun Power** (unlocked after Double Trouble). Overlay shows live `[0.00/0.05 W load=0]`.
2. From **Dispenser11** (Solar shelf): grab **Solar**, plus **Wire(s)** and a **Bulb** or **Motor**. Do **not** use a battery â€” the panel is the source.
3. Snap Solar + wires + load onto the breadboard. Placing Solar wakes **MiniatureSun**.
4. Desktop: hold Solar + **LMB/scroll** to rotate the panel; VR: pinch the panel. Face the sun until live W rises (>= 0.05) and the load is active.
5. When meter shows enough wattage + `load=1` -> stars + unlock **Induction**. Completion banner shows `Unlocked: Induction`.

### Induction (challenge 6)
1. Press **C** -> Start **Induction** (unlocked after Sun Power). Overlay shows peak EMF `[pk 0.000/0.05 V live=0.000]` (peak latched so brief Faraday spikes are not missed).
2. Go to the **Induction Lab** coil station. Grab the bar **Magnet** (lab Magnet or PHYSICS gadget).
3. Desktop: hold Magnet + **LMB/scroll** to impulse along N-S through the coil; VR: grab and throw/push the magnet through the coil bore.
4. When peak |EMF| >= 0.05 V (pk meter), objective completes -> stars + unlock **Make and Break**. Completion banner shows `Unlocked: Make and Break`. Experiment RESET clears the peak latch for a clean retry.

### Make and Break (challenge 7)
1. Press **C** -> Start **Make and Break** (unlocked after Induction). Overlay shows live `[sw=0 lit=0]`.
2. From table dispensers: grab **Battery**, **Wire(s)**, **Switch** (Dispenser3 / Switch shelf), and **Bulb**; snap onto breadboard pegs.
3. Leave the knife switch open first (bulb stays dark), then **toggle/close** the switch to complete the loop.
4. When meter shows `sw=1 lit=1` (closed placed Switch clone + lit placed Bulb clone), objective completes -> stars + unlock **Gear Up**. Completion banner shows `Unlocked: Gear Up`.

### Gear Up (challenge 8)
1. Press **C** -> Start **Gear Up** (unlocked after Make and Break). Overlay shows live `[sw=0 0/120 RPM]`.
2. From table dispensers: grab **Battery**, **Wire(s)**, **Switch** (Dispenser3 / Switch shelf), and **Motor** (Dispenser2 / Motor shelf); snap onto breadboard pegs.
3. Leave the knife switch open first (motor stays still), then **toggle/close** the switch so the motor spins.
4. When meter shows `sw=1` and RPM >= 120, objective completes -> stars + unlock **Light and Spin**. Completion banner shows `Unlocked: Light and Spin`.

### Light and Spin (challenge 9)
1. Press **C** -> Start **Light and Spin** (unlocked after Gear Up). Overlay shows live `[0/1 lit]` and `[0/120 RPM]` (both must complete).
2. From table dispensers: grab **Battery**, **Wire(s)**, **Bulb**, and **Motor** (Dispenser2 / Motor shelf); snap onto breadboard pegs (series or parallel).
3. Close the loop so the bulb lights and the motor spins; meter should show lit + RPM rising together.
4. When both objectives complete (bulb lit AND RPM >= 120), objective completes -> stars + unlock **Power Play**. Completion banner shows `Unlocked: Power Play`.

### Power Play (challenge 10)
1. Press **C** -> Start **Power Play** (unlocked after Light and Spin). Overlay shows live `[0/1 lit]` and `[0.00/0.05 W lit=0|1]`.
2. From table dispensers: grab **Battery**, **Wire(s)**, and **Bulb** (optional Motor); snap onto breadboard pegs to close a loop.
3. When the bulb lights, CircuitLab load power `P = |V| * |I|` rises (a 10 V / 1 kOhm bulb is ~0.1 W). Live W meter tracks the max across placed bulb/motor clones.
4. When both objectives complete (bulb lit AND power >= 0.05 W), objective completes -> stars + unlock **Three Lights**. Completion banner shows `Unlocked: Three Lights`.

### Three Lights (challenge 11)
1. Press **C** -> Start **Three Lights** (unlocked after Power Play). Overlay shows live `[0/3 lit]`.
2. Grab **three Bulbs** from the Bulb dispenser (restocks after each grab), plus **Battery** and **Wire(s)**.
3. Series or parallel all three so each carries significant current; only breadboard-placed clones count.
4. When meter hits `3/3 lit` -> stars + unlock **Throttle Up**. Completion banner shows `Unlocked: Throttle Up`.

### Throttle Up (challenge 12)
1. Press **C** -> Start **Throttle Up** (unlocked after Three Lights). Overlay shows live `[R=0 lit=0]` and `[0/120 RPM]`.
2. From table dispensers: grab **Battery**, **Wire(s)**, **Resistor** (Resistor shelf), **Bulb**, and **Motor** (Dispenser2 / Motor shelf); snap onto breadboard pegs.
3. Put the resistor in series so it throttles current while the bulb stays lit and the motor spins.
4. When both objectives complete (active resistor + lit bulb AND RPM >= 120), objective completes -> stars + unlock **Sun Drive**. Completion banner shows `Unlocked: Sun Drive`.

### Sun Drive (challenge 13)
1. Press **C** -> Start **Sun Drive** (unlocked after Throttle Up). Overlay shows live `[0.00/0.05 W load=0]` and `[0/120 RPM]`.
2. From **Dispenser11** grab **Solar**, from **Dispenser2** grab **Motor**, plus **Wire(s)**. Do **not** use a battery â€” the panel is the source.
3. Snap Solar + wires + Motor onto the breadboard. Rotate Solar toward **MiniatureSun** (desktop LMB/scroll; VR pinch) until wattage rises and the motor spins.
4. When solar W/load meter and RPM both pass (>= 0.05 W powering a load AND RPM >= 120), objective completes -> stars + unlock **Sun Lab**. Completion banner shows `Unlocked: Sun Lab`.

### Sun Lab (challenge 14)
1. Press **C** -> Start **Sun Lab** (unlocked after Sun Drive). Overlay shows live `[0.00/0.05 W load=0]`, `[0/1 lit]`, and `[0/120 RPM]`.
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **Bulb**, from **Dispenser2** grab **Motor**, plus **Wire(s)**. Do **not** use a battery â€” multi-load from one renewable source.
3. Snap Solar + wires + Bulb + Motor onto the breadboard (parallel preferred so both loads get voltage). Rotate Solar toward **MiniatureSun** until wattage rises, the bulb lights, and the motor spins.
4. When all three objectives complete (solar >= 0.05 W powering a load AND bulb lit AND RPM >= 120), objective completes -> stars + unlock **Sun Gate**. Completion banner shows `Unlocked: Sun Gate`.

### Sun Gate (challenge 15)
1. Press **C** -> Start **Sun Gate** (unlocked after Sun Lab). Overlay shows live `[0.00/0.05 W load=0]` and switch/lit readout.
2. From **Dispenser11** grab **Solar**, from **Dispenser3** grab **Switch**, from Bulb shelf grab **Bulb**, plus **Wire(s)**. Do **not** use a battery â€” renewable source + knife-switch control.
3. Snap Solar + Switch + wires + Bulb onto the breadboard. Leave the switch **open** first (bulb dark). Rotate Solar toward **MiniatureSun** until wattage rises, then **close** the switch so the bulb lights.
4. When both objectives complete (solar >= 0.05 W powering a load AND switch closed with bulb lit), objective completes -> stars + unlock **Sun Switch Drive**. Completion banner shows `Unlocked: Sun Switch Drive`.

### Sun Switch Drive (challenge 16)
1. Press **C** -> Start **Sun Switch Drive** (unlocked after Sun Gate). Overlay shows live `[0.00/0.05 W load=0]` and switch/RPM readout.
2. From **Dispenser11** grab **Solar**, from **Dispenser3** grab **Switch**, from **Dispenser2** grab **Motor**, plus **Wire(s)**. Do **not** use a battery â€” renewable source + knife-switch gated motor.
3. Snap Solar + Switch + wires + Motor onto the breadboard. Leave the switch **open** first (motor still). Rotate Solar toward **MiniatureSun** until wattage rises, then **close** the switch so the motor spins to 120+ RPM.
4. When both objectives complete (solar >= 0.05 W powering a load AND switch closed with motor at 120+ RPM), objective completes -> stars + unlock **Sun Switch Lab**. Completion banner shows `Unlocked: Sun Switch Lab`.

### Sun Switch Lab (challenge 17)
1. Press **C** -> Start **Sun Switch Lab** (unlocked after Sun Switch Drive). Overlay shows live `[0.00/0.05 W load=0]`, switch/lit, and switch/RPM readouts.
2. From **Dispenser11** grab **Solar**, from **Dispenser3** grab **Switch**, from Bulb shelf grab **Bulb**, from **Dispenser2** grab **Motor**, plus **Wire(s)**. Do **not** use a battery â€” renewable source + knife-switch gated multi-load.
3. Snap Solar + Switch + wires + Bulb + Motor onto the breadboard (parallel loads preferred). Leave the switch **open** first (bulb dark / motor still). Rotate Solar toward **MiniatureSun** until wattage rises, then **close** the switch so the bulb lights and the motor spins to 120+ RPM.
4. When all three objectives complete (solar >= 0.05 W powering a load AND switch closed with bulb lit AND switch closed with motor at 120+ RPM), objective completes -> stars + unlock **Sun Twin Gate**. Completion banner shows `Unlocked: Sun Twin Gate`.

### Sun Twin Gate (challenge 18)
1. Press **C** -> Start **Sun Twin Gate** (unlocked after Sun Switch Lab). Overlay shows live `[0.00/0.05 W load=0]`, `[0/2 lit]`, and switch/lit readout.
2. From **Dispenser11** grab **Solar**, from **Dispenser3** grab **Switch**, from Bulb shelf grab **two Bulbs**, plus **Wire(s)**. Do **not** use a battery â€” renewable source + knife-switch gated twin bulbs (series or parallel).
3. Snap Solar + Switch + wires + two Bulbs onto the breadboard. Leave the switch **open** first (both dark). Rotate Solar toward **MiniatureSun** until wattage rises, then **close** the switch so both bulbs light. Parallel keeps them bright; series splits voltage.
4. When all three objectives complete (solar >= 0.05 W powering a load AND 2 bulbs lit AND switch closed with bulb lit), objective completes -> stars + unlock **Sun Twin Lab**. Completion banner shows `Unlocked: Sun Twin Lab`.

### Sun Twin Lab (challenge 19)
1. Press **C** -> Start **Sun Twin Lab** (unlocked after Sun Twin Gate). Overlay shows live `[0.00/0.05 W load=0]`, `[0/2 lit]`, and switch/RPM readout.
2. From **Dispenser11** grab **Solar**, from **Dispenser3** grab **Switch**, from Bulb shelf grab **two Bulbs**, from **Dispenser2** grab **Motor**, plus **Wire(s)**. Do **not** use a battery â€” renewable source + knife-switch gated twin bulbs and motor.
3. Snap Solar + Switch + wires + two Bulbs + Motor onto the breadboard (parallel loads preferred). Leave the switch **open** first (bulbs dark / motor still). Rotate Solar toward **MiniatureSun** until wattage rises, then **close** the switch so both bulbs light and the motor spins to 120+ RPM.
4. When all three objectives complete (solar >= 0.05 W powering a load AND 2 bulbs lit AND switch closed with motor at 120+ RPM), objective completes -> stars + unlock **Sun Throttle**. Completion banner shows `Unlocked: Sun Throttle`.

### Sun Throttle (challenge 20)
1. Press **C** -> Start **Sun Throttle** (unlocked after Sun Twin Lab). Overlay shows live `[0.00/0.05 W load=0]`, `[R=0 lit=0]`, and switch/RPM readout.
2. From **Dispenser11** grab **Solar**, from Resistor shelf grab **Resistor**, from **Dispenser3** grab **Switch**, from Bulb shelf grab **Bulb**, from **Dispenser2** grab **Motor**, plus **Wire(s)**. Do **not** use a battery â€” renewable Throttle Up: series resistor + lit bulb + knife-switch gated motor.
3. Snap Solar + Resistor + Switch + wires + Bulb + Motor onto the breadboard. Put the resistor in series so it throttles current while the bulb stays lit. Leave the switch **open** first (motor still). Rotate Solar toward **MiniatureSun** until wattage rises, then **close** the switch so the motor spins to 120+ RPM.
4. When all three objectives complete (solar >= 0.05 W powering a load AND resistor in circuit with lit bulb AND switch closed with motor at 120+ RPM), objective completes -> stars + unlock **Sun Power Lab**. Completion banner shows `Unlocked: Sun Power Lab`.

### Sun Power Lab (challenge 21)
1. Press **C** -> Start **Sun Power Lab** (unlocked after Sun Throttle). Overlay shows live `[0.00/0.05 W load=0]`, circuit power `[0.00/0.05 W lit=0]`, and switch/RPM readout.
2. From **Dispenser11** grab **Solar**, from **Dispenser3** grab **Switch**, from Bulb shelf grab **Bulb**, from **Dispenser2** grab **Motor**, plus **Wire(s)**. Do **not** use a battery â€” renewable Power Play: measurable load watts (P = |V| * |I|) + knife-switch gated motor.
3. Snap Solar + Switch + wires + Bulb + Motor onto the breadboard. Leave the switch **open** first (motor still). Rotate Solar toward **MiniatureSun** until wattage rises and load power hits >= 0.05 W, then **close** the switch so the motor spins to 120+ RPM.
4. When all three objectives complete (solar >= 0.05 W powering a load AND circuit load power >= 0.05 W AND switch closed with motor at 120+ RPM), objective completes -> stars + unlock **Sun Induction**. Completion banner shows `Unlocked: Sun Induction`.

### Sun Induction (challenge 22)
1. Press **C** -> Start **Sun Induction** (unlocked after Sun Power Lab). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and switch/RPM readout.
2. Build a solar circuit from **Dispenser11** **Solar**, **Dispenser3** **Switch**, Bulb and/or **Dispenser2** **Motor**, plus **Wire(s)** (no battery). At the Induction Lab coil station, grab the bar magnet.
3. Face **MiniatureSun** until solar powers a load, thrust the magnet through the coil until peak |EMF| >= 0.05 V, then **close** the knife switch so the motor spins to 120+ RPM.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND switch closed with motor at 120+ RPM), objective completes -> stars + unlock **Sun Faraday Lab**. Completion banner shows Unlocked: Sun Faraday Lab.

### Sun Faraday Lab (challenge 23)
1. Press **C** -> Start **Sun Faraday Lab** (unlocked after Sun Induction). Overlay shows live [0.00/0.05 W load=0], peak EMF [pk 0.000/0.05 V live=0.000], and circuit power [0.00/0.05 W lit=0].
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **Bulb** and/or from **Dispenser2** grab **Motor**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Lab: measurable load watts (P = |V| * |I|) + Induction Lab peak EMF.
3. Snap Solar + wires + Bulb/Motor onto the breadboard. Face **MiniatureSun** until solar powers a load and circuit power hits >= 0.05 W. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND circuit load power >= 0.05 W), objective completes -> stars + unlock **Sun Faraday Drive**. Completion banner shows `Unlocked: Sun Faraday Drive`.

### Sun Faraday Drive (challenge 24)
1. Press **C** -> Start **Sun Faraday Drive** (unlocked after Sun Faraday Lab). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and resistor `[R=0 lit=0]`.
2. From **Dispenser11** grab **Solar**, from Resistor shelf grab **Resistor**, from Bulb shelf grab **Bulb**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Drive: series resistor throttles current with bulb lit + Induction Lab peak EMF.
3. Snap Solar + Resistor + wires + Bulb onto the breadboard (resistor in series). Face **MiniatureSun** until solar powers a load and the bulb stays lit with the resistor in circuit. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND resistor in circuit with lit bulb), objective completes -> stars + unlock **Sun Faraday Gate**. Completion banner shows `Unlocked: Sun Faraday Gate`.

### Sun Faraday Gate (challenge 25)
1. Press **C** -> Start **Sun Faraday Gate** (unlocked after Sun Faraday Drive). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and switch/bulb `[sw=0 lit=0]`.
2. From **Dispenser11** grab **Solar**, from **Dispenser3** grab **Switch**, from Bulb shelf grab **Bulb**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Gate: knife-switch gated lit bulb + Induction Lab peak EMF.
3. Snap Solar + Switch + wires + Bulb onto the breadboard. Face **MiniatureSun** until solar powers a load; keep the switch open first (bulb dark), then **close** so the bulb lights. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND switch closed with bulb lit), objective completes -> stars + unlock **Sun Faraday Twin**. Completion banner shows `Unlocked: Sun Faraday Twin`.

### Sun Faraday Twin (challenge 26)
1. Press **C** -> Start **Sun Faraday Twin** (unlocked after Sun Faraday Gate). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and twin bulbs `[0/2 lit]`.
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **two Bulbs**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Twin: two bulbs lit from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + two Bulbs onto the breadboard (parallel preferred). Face **MiniatureSun** until solar powers a load and both bulbs light. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND 2 bulbs lit), objective completes -> stars + unlock **Sun Faraday Trio**. Completion banner shows `Unlocked: Sun Faraday Trio`.

### Sun Faraday Trio (challenge 27)
1. Press **C** -> Start **Sun Faraday Trio** (unlocked after Sun Faraday Twin). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and trio bulbs `[0/3 lit]`.
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **three Bulbs**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Trio: three bulbs lit from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + three Bulbs onto the breadboard (parallel preferred). Face **MiniatureSun** until solar powers a load and all three bulbs light. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND 3 bulbs lit), objective completes -> stars + unlock **Sun Faraday Spin**. Completion banner shows `Unlocked: Sun Faraday Spin`.

### Sun Faraday Spin (challenge 28)
1. Press **C** -> Start **Sun Faraday Spin** (unlocked after Sun Faraday Trio). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and motor RPM `[0/120 RPM]`.
2. From **Dispenser11** grab **Solar**, from **Dispenser2** grab **Motor**, plus **Wire(s)**. Do **not** use a battery - renewable Faraday Spin: motor at 120+ RPM from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + Motor onto the breadboard. Face **MiniatureSun** until solar powers a load and the motor reaches 120+ RPM. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND motor at 120+ RPM), objective completes -> stars + unlock **Sun Faraday Quad**. Completion banner shows `Unlocked: Sun Faraday Quad`.
### Sun Faraday Quad (challenge 29)
1. Press **C** -> Start **Sun Faraday Quad** (unlocked after Sun Faraday Spin). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and quad bulbs `[0/4 lit]`.
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **four Bulbs**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Quad: four bulbs lit from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + four Bulbs onto the breadboard (parallel preferred). Face **MiniatureSun** until solar powers a load and all four bulbs light. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND 4 bulbs lit), objective completes -> stars + unlock **Sun Faraday Penta**. Completion banner shows `Unlocked: Sun Faraday Penta`.
### Sun Faraday Penta (challenge 30)
1. Press **C** -> Start **Sun Faraday Penta** (unlocked after Sun Faraday Quad). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and penta bulbs `[0/5 lit]`.
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **five Bulbs**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Penta: five bulbs lit from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + five Bulbs onto the breadboard (parallel preferred). Face **MiniatureSun** until solar powers a load and all five bulbs light. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND 5 bulbs lit), objective completes -> stars + unlock **Sun Faraday Hex**. Completion banner shows `Unlocked: Sun Faraday Hex`.
### Sun Faraday Hex (challenge 31)
1. Press **C** -> Start **Sun Faraday Hex** (unlocked after Sun Faraday Penta). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and hex bulbs `[0/6 lit]`.
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **six Bulbs**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Hex: six bulbs lit from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + six Bulbs onto the breadboard (parallel preferred). Face **MiniatureSun** until solar powers a load and all six bulbs light. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND 6 bulbs lit), objective completes -> stars + unlock **Sun Faraday Hept**. Completion banner shows `Unlocked: Sun Faraday Hept`.
### Sun Faraday Hept (challenge 32)
1. Press **C** -> Start **Sun Faraday Hept** (unlocked after Sun Faraday Hex). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and hept bulbs `[0/7 lit]`.
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **seven Bulbs**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Hept: seven bulbs lit from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + seven Bulbs onto the breadboard (parallel preferred). Face **MiniatureSun** until solar powers a load and all seven bulbs light. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND 7 bulbs lit), objective completes -> stars + unlock **Sun Faraday Oct**. Completion banner shows `Unlocked: Sun Faraday Oct`.
### Sun Faraday Oct (challenge 33)
1. Press **C** -> Start **Sun Faraday Oct** (unlocked after Sun Faraday Hept). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and oct bulbs `[0/8 lit]`.
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **eight Bulbs**, plus **Wire(s)**. Do **not** use a battery - renewable Faraday Oct: eight bulbs lit from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + eight Bulbs onto the breadboard (parallel preferred). Face **MiniatureSun** until solar powers a load and all eight bulbs light. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND 8 bulbs lit), objective completes -> stars + unlock **Sun Faraday Nona**. Completion banner shows `Unlocked: Sun Faraday Nona`.
### Sun Faraday Nona (challenge 34)
1. Press **C** -> Start **Sun Faraday Nona** (unlocked after Sun Faraday Oct). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and nona bulbs `[0/9 lit]`.
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **nine Bulbs**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Nona: nine bulbs lit from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + nine Bulbs onto the breadboard (parallel preferred). Face **MiniatureSun** until solar powers a load and all nine bulbs light. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND 9 bulbs lit), objective completes -> stars + unlock **Sun Faraday Deca**. Completion banner shows `Unlocked: Sun Faraday Deca`.
### Sun Faraday Deca (challenge 35)
1. Press **C** -> Start **Sun Faraday Deca** (unlocked after Sun Faraday Nona). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and deca bulbs `[0/10 lit]`.
2. From **Dispenser11** grab **Solar**, from Bulb shelf grab **ten Bulbs**, plus **Wire(s)**. Do **not** use a battery â€” renewable Faraday Deca: ten bulbs lit from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + ten Bulbs onto the breadboard (parallel preferred). Face **MiniatureSun** until solar powers a load and all ten bulbs light. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND 10 bulbs lit), objective completes -> stars + unlock **Sun Faraday Boost**. Completion banner shows `Unlocked: Sun Faraday Boost`.
### Sun Faraday Boost (challenge 36)
1. Press **C** -> Start **Sun Faraday Boost** (unlocked after Sun Faraday Deca). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and circuit power `[0.00/0.25 W lit=0]`.
2. From **Dispenser11** grab **Solar**, from Bulb/Motor shelves grab a **Bulb** and/or **Motor**, plus **Wire(s)**. Do **not** use a battery - renewable Faraday Boost: high load power from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + Bulb/Motor onto the breadboard. Face **MiniatureSun** and aim the panel for strong irradiance until solar powers a load and load P=|V|*|I| reaches >= 0.25 W. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND circuit power >= 0.25 W), objective completes -> stars + unlock **Sun Faraday Surge**. Completion banner shows `Unlocked: Sun Faraday Surge`.
### Sun Faraday Surge (challenge 37)
1. Press **C** -> Start **Sun Faraday Surge** (unlocked after Sun Faraday Boost). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and circuit power `[0.00/0.50 W lit=0]`.
2. From **Dispenser11** grab **Solar**, from Bulb/Motor shelves grab a **Bulb** and/or **Motor**, plus **Wire(s)**. Do **not** use a battery - renewable Faraday Surge: higher load power from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + Bulb/Motor onto the breadboard. Face **MiniatureSun** and aim the panel for strong irradiance until solar powers a load and load P=|V|*|I| reaches >= 0.5 W. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND circuit power >= 0.5 W), objective completes -> stars + unlock **Sun Faraday Peak**. Completion banner shows `Unlocked: Sun Faraday Peak`.
### Sun Faraday Peak (challenge 38)
1. Press **C** -> Start **Sun Faraday Peak** (unlocked after Sun Faraday Surge). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and circuit power `[0.00/1.00 W lit=0]`.
2. From **Dispenser11** grab **Solar**, from Bulb/Motor shelves grab a **Bulb** and/or **Motor**, plus **Wire(s)**. Do **not** use a battery - renewable Faraday Peak: peak load power from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + Bulb/Motor onto the breadboard. Face **MiniatureSun** and aim the panel for strong irradiance until solar powers a load and load P=|V|*|I| reaches >= 1.0 W. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND circuit power >= 1.0 W), objective completes -> stars + unlock **Sun Faraday Apex**. Completion banner shows `Unlocked: Sun Faraday Apex`.
### Sun Faraday Apex (challenge 39)
1. Press **C** -> Start **Sun Faraday Apex** (unlocked after Sun Faraday Peak). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and circuit power `[0.00/2.00 W lit=0]`.
2. From **Dispenser11** grab **Solar**, from Bulb/Motor shelves grab a **Bulb** and/or **Motor**, plus **Wire(s)**. Do **not** use a battery - renewable Faraday Apex: apex load power from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + Bulb/Motor onto the breadboard. Face **MiniatureSun** and aim the panel for strong irradiance until solar powers a load and load P=|V|*|I| reaches >= 2.0 W. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND circuit power >= 2.0 W), objective completes -> stars + unlock **Sun Faraday Nova**. Completion banner shows `Unlocked: Sun Faraday Nova`.
### Sun Faraday Nova (challenge 40)
1. Press **C** -> Start **Sun Faraday Nova** (unlocked after Sun Faraday Apex). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and circuit power `[0.00/4.00 W lit=0]`.
2. From **Dispenser11** grab **Solar**, from Bulb/Motor shelves grab a **Bulb** and/or **Motor**, plus **Wire(s)**. Do **not** use a battery - renewable Faraday Nova: nova load power from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + Bulb/Motor onto the breadboard. Face **MiniatureSun** and aim the panel for strong irradiance until solar powers a load and load P=|V|*|I| reaches >= 4.0 W. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND circuit power >= 4.0 W), objective completes -> stars + unlock **Sun Faraday Quasar**. Completion banner shows `Unlocked: Sun Faraday Quasar`.
### Sun Faraday Quasar (challenge 41)
1. Press **C** -> Start **Sun Faraday Quasar** (unlocked after Sun Faraday Nova). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and circuit power `[0.00/8.00 W lit=0]`.
2. From **Dispenser11** grab **Solar**, from Bulb/Motor shelves grab a **Bulb** and/or **Motor**, plus **Wire(s)**. Do **not** use a battery - renewable Faraday Quasar: quasar load power from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + Bulb/Motor onto the breadboard. Face **MiniatureSun** and aim the panel for strong irradiance until solar powers a load and load P=|V|*|I| reaches >= 8.0 W. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND circuit power >= 8.0 W), objective completes -> stars + unlock **Sun Faraday Pulsar**. Completion banner shows `Unlocked: Sun Faraday Pulsar`.
### Sun Faraday Pulsar (challenge 42)
1. Press **C** -> Start **Sun Faraday Pulsar** (unlocked after Sun Faraday Quasar). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and circuit power `[0.00/16.00 W lit=0]`.
2. From **Dispenser11** grab **Solar**, from Bulb/Motor shelves grab a **Bulb** and/or **Motor**, plus **Wire(s)**. Do **not** use a battery - renewable Faraday Pulsar: pulsar load power from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + Bulb/Motor onto the breadboard. Face **MiniatureSun** and aim the panel for strong irradiance until solar powers a load and load P=|V|*|I| reaches >= 16.0 W. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND circuit power >= 16.0 W), objective completes -> stars + unlock **Sun Faraday Magnetar**. Completion banner shows `Unlocked: Sun Faraday Magnetar`.
### Sun Faraday Magnetar (challenge 43)
1. Press **C** -> Start **Sun Faraday Magnetar** (unlocked after Sun Faraday Pulsar). Overlay shows live `[0.00/0.05 W load=0]`, peak EMF `[pk 0.000/0.05 V live=0.000]`, and circuit power `[0.00/32.00 W lit=0]`.
2. From **Dispenser11** grab **Solar**, from Bulb/Motor shelves grab a **Bulb** and/or **Motor**, plus **Wire(s)**. Do **not** use a battery - renewable Faraday Magnetar: magnetar load power from the panel + Induction Lab peak EMF.
3. Snap Solar + wires + Bulb/Motor onto the breadboard. Face **MiniatureSun** and aim the panel for strong irradiance until solar powers a load and load P=|V|*|I| reaches >= 32.0 W. At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V.
4. When all three objectives complete (solar >= 0.05 W powering a load AND peak |EMF| >= 0.05 V AND circuit power >= 32.0 W), objective completes -> stars. End of starter campaign (campaign deepest / next unlock N/A).
### Challenge RESET
While a challenge is active, **Esc/P -> Reset Experiment** (or SimChip / Toolbelt New Run) clears CircuitLab **and** sticky challenge objectives + elapsed timer, keeping the same challenge active for a clean retry.
## Shipping only

- **Ctrl+B** / Build and Run ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ Android (Quest) or Windows player.
- Quest: Developer Mode + authorized `adb devices` before APK install.
- Do **not** build APK unless `adb devices` shows the authorized Quest serial you intend to flash.

## Never

- Second Unity on this project
- `GetInstanceID` in tooling paths
- `Sprites/Default` for Giza/lab surfaces
- Disabling MountainScene
- Assuming Ctrl+P installs to the headset (it does not)

