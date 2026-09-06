# QHYSICS — how to Play (Editor)

**Daily testing = Ctrl+P (Play).** Do **not** use Ctrl+B for daily runs — that builds an APK/player.
**Ctrl+P ≠ APK.** Headset view needs **Meta Quest Link** (or Device Simulator on desktop).

## One Editor only

- Project: `C:\Users\KING\projects\QHYSICS`
- Branch: `reality-engine`
- Unity: **6000.7.0a4** (one instance — never a second Editor on this project)

## Who is the player?

**XR Origin IS the player** (camera + hands under Camera Offset). There is no separate humanoid avatar. If the headset shows nothing, you are not linked / not simulating — not missing a character mesh.

## Play steps — Meta Quest Link (Quest 3S)

1. On PC: install/open **Meta Quest Link** (Air Link or cable). Quest in Developer Mode, same account, PC allowed.
2. Put on headset → enable **Link** / connect to this PC. Confirm Link status is Connected.
3. In Unity (only one Editor): open `Assets/Scenes/Faraday.unity`.
4. Optional once: **Reality Engine → Fix Player Spawn** (or **Reset Player at Lab**) then **Ctrl+S** — parks XR Origin on LabPlaza north of the circuit table facing Khufu, enables Main Camera, wires locomotion XR Origin.
5. Press **Ctrl+P** (Play). Game view mirrors the HMD via OpenXR + Link.
6. First run: world **Main Menu → Enter Sandbox** (or skip if already entered).
7. Interact: teleport / smooth move on plaza, grab circuit parts, Toolbelt **M / Tab / Menu**, Pause **Esc / P**.

If Play works in Editor Game view but the headset stays on the Quest home / black: Link is not active — fix Link first. Do not build APK for daily testing.

## Play steps — XR Device Simulator (desktop, no headset)

1. Package Manager → **XR Interaction Toolkit** → Samples → import **XR Device Simulator** (once).
2. Project Settings → **XR Plug-in Management** → **XR Interaction Toolkit** → enable **Use XR Device Simulator in scenes** / auto-instantiate (or add the `XR Device Simulator` prefab to Faraday).
   - Asset: `Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` — set simulator prefab after sample import; `Automatically Instantiate Simulator Prefab` can be on for Editor-only.
3. Open Faraday → **Ctrl+P**. Use keyboard/mouse per simulator HUD (move/look/grip).
4. Optional: **Reality Engine → Fix Player Spawn** before Play if Origin pose looks wrong in Hierarchy.

## Play steps — checklist

1. Open `Assets/Scenes/Faraday.unity` (Build Settings already has Faraday enabled).
2. Confirm Hierarchy has **LabLandscape** (Giza) and **QhysicsUI** (or RealityEngine host). If missing:
   - Menu **Reality Engine → Place Giza Complex**
   - Menu **Reality Engine → Place QHYSICS UI**
   - **Ctrl+S** / File → Save so placement persists.
3. Optional: **Reality Engine → Fix Player Spawn** (XR Origin on plaza, Main Camera MainCamera+enabled, facing Khufu).
4. Press **Ctrl+P** (or the Play button).
5. Headset: Quest Link / OpenXR, **or** Editor with **XR Device Simulator**.
6. First run: world **Main Menu → Enter Sandbox** (or skip if already entered). Short onboarding tips are dismissible (Next / Skip).
7. Interact:
   - Move with XRI locomotion / teleport on plaza
   - Grab circuit parts from table dispensers (grip)
   - Build Battery → Wire → Bulb → Switch loop
   - **M / Tab / Menu / B / Grip** toggles Toolbelt
   - **Esc / P** opens Pause → Resume / Reset Experiment / Settings / Exit Play (Editor)
   - SimChip (`> 1x`) cycles sim speed / pause / reset (Reset also calls CircuitLab.Reset)

## Shipping only

- **Ctrl+B** / Build and Run → Android (Quest) or Windows player.
- Quest: Developer Mode + authorized `adb devices` before APK install.
- Do **not** build APK unless `adb devices` shows the authorized Quest serial you intend to flash.

## Never

- Second Unity on this project
- `GetInstanceID` in tooling paths
- `Sprites/Default` for Giza/lab surfaces
- Disabling MountainScene
- Assuming Ctrl+P installs to the headset (it does not)
