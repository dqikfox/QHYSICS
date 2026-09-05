# QHYSICS daily run (Editor)

**Do not use Ctrl+B for daily testing.** Ctrl+B / Build and Run builds an Android APK or Windows player (shipping only). Quest ADB must be authorized for APK installs.

## Daily procedure

1. Open `C:\Users\KING\projects\QHYSICS` in **Unity 6000.7.0a4** (one Editor only — never a second Unity).
2. Open scene `Assets/Scenes/Faraday.unity` if it is not already open.
3. Hierarchy should already contain **LabLandscape** (Giza) and **QHYSICS UI** after Place + Save. If the scene looks empty:
   - Menu **Reality Engine → Place Giza Complex**
   - Menu **Reality Engine → Place QHYSICS UI**
   - **File → Save** (or Ctrl+S) so they persist next session.
4. Play with **Ctrl+P** (or the Play button) using **XR Device Simulator** or **Quest Link**.
5. Exit Play before placing or editing scene content.

## Shipping only

- **Ctrl+B** / File → Build and Run → Android APK (Quest) or Windows player.
- Quest: enable Developer Mode; `adb devices` must show an authorized device before APK install.

## Never

- Second Unity instance on this project
- `GetInstanceID` in tooling paths
- `Sprites/Default` materials for Giza/lab surfaces
- Push without an explicit ask
