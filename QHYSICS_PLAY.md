# QHYSICS — how to Play (Editor)

**Daily testing = Ctrl+P (Play).** Do **not** use Ctrl+B for daily runs — that builds an APK/player.

## One Editor only

- Project: `C:\Users\KING\projects\QHYSICS`
- Branch: `reality-engine`
- Unity: **6000.7.0a4** (one instance — never a second Editor on this project)

## Play steps

1. Open `Assets/Scenes/Faraday.unity` (Build Settings already has Faraday enabled).
2. Confirm Hierarchy has **LabLandscape** (Giza) and **QhysicsUI** (or RealityEngine host). If missing:
   - Menu **Reality Engine → Place Giza Complex**
   - Menu **Reality Engine → Place QHYSICS UI**
   - **Ctrl+S** / File → Save so placement persists.
3. Optional: **Reality Engine → Reset Player at Lab** (XR Origin on plaza, facing Khufu / circuit table).
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

## Never

- Second Unity on this project
- `GetInstanceID` in tooling paths
- `Sprites/Default` for Giza/lab surfaces
- Disabling MountainScene