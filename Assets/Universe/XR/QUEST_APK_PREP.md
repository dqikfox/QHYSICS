# Quest native APK path (prep only)

Sideload prep for Reality Engine on Meta Quest 3S via OpenXR. Not a store submit.

## Already configured
- XR Plug-in Management Android: OpenXR loader
- OpenXR Android: Meta Quest Support (Quest / 2 / Pro / 3 / 3S), Meta XR Feature, Oculus Touch + Meta Quest Touch Plus
- Meta Quest Build Profile: IL2CPP, ARM64, min API 29, target API 32, Development build on for first install

## Package identity (this slice)
- Company: `dqikfox`
- Product: `Reality Engine`
- Android package: `com.dqikfox.realityengine`

## Build
1. Unity already open on Faraday — do not launch a second Editor
2. File → Build Profiles → select **Meta Quest**
3. Scenes: `Assets/Scenes/Faraday.unity` enabled
4. Build APK to `Builds/RealityEngine.apk` (create `Builds/` if needed)

## Install check
1. Quest USB debugging authorized (`adb devices` must show `device`, not `unauthorized`)
2. `adb install -r Builds/RealityEngine.apk`
3. Launch on headset; confirm both controllers + floor origin

## Blockers noted 2026-09-04
- Headset `3487C10H4805H1` was `unauthorized` over ADB — accept the USB prompt in-headset before install
