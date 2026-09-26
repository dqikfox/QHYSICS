# QHYSICS — Chapter 1 Gap Review

_Branch `reality-engine`, reviewed 2026-09-26 (AEST). Unity 6000.7.0a4 / URP / OpenXR+XRI, target Meta Quest 3S (Link and standalone)._

**Bottom line:** QHYSICS has a lot of simulation breadth: a SPICE breadboard, Faraday-Lenz induction, about 59 `Load*Gadget` tools and a 43-step challenge chain. It does not have a **chapter** yet. There is no narrative, no authored level sequence, no chapter select, no ending, no challenge audio, and no build has been tested on hardware. The campaign is also a single linear chain with no chapter structure. Past challenge 12 it becomes machine-generated permutations (Sun × Faraday × N bulbs × W targets), and the top power targets are probably impossible to reach. Chapter 1 should be a **curated subset** (8 levels) of what already exists, with intro, ending, audio and a verified build around it.

---

## 1. What exists (verified in code)

| Area | Status | Where |
|---|---|---|
| Circuit sim | ✅ SpiceSharp breadboard 9×9, Battery 10 V, Bulb 1 kΩ, Motor 2 kΩ, Resistor 470 Ω, Solar ≤1 W/10 V | `Assets/Scripts/CircuitLab/*.cs` (`CircuitLab.cs` numRows/numCols = 9) |
| Induction lab | ✅ InductionCoil + MagneticDipole + InductionCircuit, lab auto-bind | `Assets/Universe/Experiments/InductionLabBootstrap.cs`, `Physics/Electromagnetism/` |
| Gadgets | ✅ 59 `Load*Gadget.cs`. All are wired: spawn key in `QhysicsGadgets.SpawnByLabel`, toolbelt chip in `QhysicsToolbelt.ChipSets`, root→label in `LabSandboxSave.BuildRootMap` (checked by script on 2026-09-26: 0 missing) | `Assets/Universe/Player/` |
| Campaign engine | ✅ 43 challenges, 9 objective types, prerequisite unlock chain, stars (time / component count), live readouts, peak-EMF latch, RESET | `Assets/Universe/Challenges/ChallengeManager.cs` (~2.4k lines), `ChallengeDefinition.cs` |
| Campaign save | ✅ JSON `persistentDataPath/RealityEngine/challenges/progress.json` (stars, best time, best count, unlocked) | `ChallengeProgress.cs` |
| Sandbox save | ✅ F5/F9 + EXPERIMENTS Save/Load, slot0 gadget poses only (not breadboard snaps, not challenge state) | `Assets/Universe/Systems/LabSandboxSave.cs` |
| Challenge UI | ⚠️ List (C key), objective overlay, "CHALLENGE COMPLETE!" banner with stars | `ChallengeUi.cs` |
| Main menu | ⚠️ One world-space panel: "Enter Sandbox" + "Settings". Shows only on first ever launch (`PlayerPrefs QHYSICS.MainMenu.EnteredOnce`) | `Assets/Universe/UI/QhysicsMainMenu.cs` |
| Onboarding | ⚠️ One dismissable text strip ("BOOT → ENTER → INTERACT → EXPERIMENT") | `QhysicsOnboarding.cs` |
| Pause / settings | ✅ Pause panel, settings panel (volume via `AudioRouter`) | `QhysicsPausePanel.cs`, `QhysicsSettingsPanel.cs` |
| Mentor | ⚠️ `Scientist.cs` reads `ChallengeManager.ChallengeContext` (local, no network) | `Assets/Universe/AI/` |
| World | ✅ Giza complex, MountainScene, plaza spawn, stations hub | `Visualization/LabWorld/GizaComplex.cs`, `Stations/LabStationHub.cs` |
| Player | ✅ Desktop (WASD/hotbar/crosshair) + XR hands, toolbelt, hip pouch, operators, training combat | `Assets/Universe/Player/`, `XR/` |
| Juice | ✅ Bulb glow, circuit juice, haptics | `Assets/Juice/` |

## 2. Gaps: what a shippable Chapter 1 still needs

### 2.1 Narrative and structure (missing)
- **No chapter concept in code.** `rg -i chapter Assets/Universe` returns nothing. `ChallengeManager.Campaign` is one flat array with one prerequisite chain (first_light → … → sun_faraday_magnetar).
- **No theme or story framing.** Challenge descriptions are instructions ("close a battery-bulb circuit"). Nothing connects them to the project pitch ("manipulate the systems that produce facts"), to Faraday, or to the Giza setting.
- **The curve past challenge 12 is procedurally padded.** Challenges 13–43 are permutations of `SolarPoweringLoad + InducedEmfThreshold + {N bulbs | W | switch | RPM}`. Names like Quad/Penta/…/Deca and Boost/Surge/…/Magnetar carry no new idea. Only ~6 of them teach something new.
- **Objectives are sticky, not simultaneous.** `EvaluateObjectives()` latches `obj.completed = true` for good. "Solar AND EMF AND 32 W" can be done one objective at a time with three unrelated circuits, so the combo challenges don't test what their names suggest.

### 2.2 Unbeatable or unverified challenges
Per-load power is P = V²/R. One 10 V battery across a 1 kΩ bulb gives **0.1 W**. The requirements work out as:

| Ch | id | Target | Series 10 V batteries needed across one bulb | Verdict |
|---|---|---|---|---|
| 36 | sun_faraday_boost | 0.25 W | 2 | OK |
| 37 | sun_faraday_surge | 0.5 W | 3 | OK |
| 38 | sun_faraday_peak | 1 W | 4 | OK, tight |
| 39 | sun_faraday_apex | 2 W | 5 | Hard |
| 40 | sun_faraday_nova | 4 W | 7 | Hard on 9×9 |
| 41 | sun_faraday_quasar | 8 W | 9 | Probably not reachable |
| 42 | sun_faraday_pulsar | 16 W | 13 | Probably not reachable |
| 43 | sun_faraday_magnetar | 32 W | 18 (≈179 V) | Geometrically feasible but tedious (see correction below) |

**Correction (2026-09-27):** CircuitLab parts are all length 1 on the peg grid (only `LongWire` is length 2, see `PegMgr.cs`). A series loop of 18 batteries + 1 bulb is 19 edges, which fits inside the 9×9 board's 32-edge perimeter. So 32 W is **possible but tedious**, not impossible. Math: P = (10·N)² / 1000 Ω per bulb, so N = ⌈√(P·1000)/10⌉: 0.25 W→2, 0.5→3, 1→4, 2→5, 4→7, 8→9, 16→13, 32→18 batteries. Motor = 2 kΩ, resistor = 470 Ω, battery = 10 V.

Solar panels max out at 1 W (10 V), so they don't help. Challenges 34–35 (9 and 10 bulbs lit) are also unverified on a 9×9 board. **None of challenges 13–43 has a recorded Play-mode clear.**

### 2.3 Onboarding and menus
- `QhysicsMainMenu` has no **Continue / Chapter Select / Credits / Quit**. After the first launch it never shows again, so a returning player has no way back to a menu.
- **No VR way to open challenges.** `ChallengeUi.HandleInput()` only responds to the keyboard `C` key. There is no toolbelt chip, pause-menu entry or controller binding, so a Quest player can't open the campaign at all.
- **The challenge list doesn't scroll.** The comment says "Scrollable content area" but there is no `ScrollRect` or mask. 43 entries × 90 px go into a 600 px panel, so roughly entries 7+ are unreadable or unclickable.
- The onboarding strip is text only. There is no interactive tutorial (grab → place on breadboard → close loop → read meter), and it only appears once.

### 2.4 Progression and save
- Campaign save works, but there is **no chapter completion state**, no "resume at next level", and no reset-progress UI (only the in-level RESET).
- `LabSandboxSave` explicitly does not save breadboard snaps, so a player can't save a half-built circuit.
- **No win or completion screen for a chapter.** The only feedback is the per-challenge banner. There are no summary stars, no outro, and no route to the next chapter.

### 2.5 Audio
- **The mixer is not reachable at runtime.** `AudioRouter.LoadMixer()` loads `Resources/QhysicsAudioMixerRef` or `Resources/QhysicsMixer`. Neither exists: `Assets/Sounds/QhysicsMixer.mixer` is outside `Resources` and still **untracked in git**, and there is no `QhysicsAudioMixerRef` asset. So the settings volume slider can't route through the mixer, and the log shows the "[AudioRouter] Could not load QhysicsMixer" warning.
- **No challenge or UI audio.** `ChallengeManager` and `ChallengeUi` don't play any sounds: nothing on objective complete, challenge complete, unlock, or button press. Usable clips already exist in `Assets/Sounds` (`Bell.wav`, `ButtonPress.wav`, `CircuitClick.wav`, `Pop.wav`, `ForceField.wav`).
- No music or ambience plan for the chapter. Ambience clips exist (`ForestWind`, `FlowingRiver`) but nothing is sequenced.

### 2.6 VR comfort and performance
- `ProjectSettings.asset`: `m_StereoRenderingPath: 0` is Multi-Pass. Switch to Single Pass Instanced for Quest.
- `Faraday.unity` is **~118 MB with ~6.8k serialized YAML objects** (Giza, biology, chemistry, survey, 59 gadget types). There is no Quest standalone perf budget or profile. The plan file lists "light bake / end-to-end profiling" as not done.
- No comfort options were checked beyond the Faraday defaults: vignette on smooth move, snap-turn angle, seated mode, dominant hand.
- 59 gadgets were each added in one commit with no Play-mode or XR-grab test recorded. Treat them as **untested** until a smoke pass has been done.

### 2.7 Build and release
- `productName: Faraday`, `companyName: DefaultCompany`, `bundleVersion: 0.1`. Chapter 1 needs its own identity and version.
- Build settings have only `Faraday.unity` enabled (`Basic.unity` is disabled). There's no build folder and no recorded **Windows build or Quest Link test**, and no Android APK build.
- Git LFS: **the GitHub repo is over its LFS budget.** Pushing the updated `Faraday.unity` fails with "This repository exceeded its LFS budget". Every scene save needs LFS quota, so this blocks the team workflow.
- Uncommitted drift is waiting for a decision: `Packages/manifest.json` (ai.assistant 2.18→2.20-pre), `OpenXR Package Settings.asset` (feature list entries removed, Touch Pro profile enabled), `ProjectAuditorSettings.asset`, TMP fallback atlas.
- Text polish: mojibake (double-encoded `—`, `→`, `°`) in `QHYSICS_PLAY.md` headings, `ChallengeManager.cs` comments, `GizaComplex.cs` (including a `Contains("kawÃ¡b")` string match), `QhysicsGadgets.cs` comments.

---

## 3. Proposed Chapter 1: "Faraday's Bench — Making Light from Motion"

**Theme:** electromagnetism, from a closed loop to induction. The player isn't *told* Ohm's or Faraday's law. They change the system (loop, resistance, motion, flux) and watch the measured fact appear on a meter. Setting: the induction lab bench at the Giza plaza, framed as a "bench notebook" of Faraday's 1831 experiments. The Giza plaza is the hub, and later chapters (mechanics, optics, fluids) can use the PHYSICS gadgets that already exist.

| # | Level (id) | Reuses | The fact the player *produces* | Gadgets / meters introduced |
|---|---|---|---|---|
| 0 | **Bench Orientation** (new, tutorial) | onboarding strip, toolbelt | grab, place, snap, open toolbelt, read a meter | Hand, Toolbelt, Multimeter |
| 1 | **First Light** (`first_light`) | ch1 | a closed loop is needed for current | Battery, Wire, Bulb |
| 2 | **Make and Break** (`make_and_break`) | ch7 | a switch controls the loop | Switch |
| 3 | **Current Control** (`current_control`) | ch2 | resistance limits current (read I drop) | Resistor, Ammeter |
| 4 | **Double Trouble** (`double_trouble`) | ch4 | series vs parallel brightness | 2 Bulbs, Voltmeter |
| 5 | **Spin Up** (`spin_up`) | ch3 | current makes motion (motor) | Motor |
| 6 | **Lines of Force** (new) | Compass + Magnet/Dipole + Field Lens + Probe | a magnet has a field you can map | Compass, Field Lens, Probe |
| 7 | **Induction** (`induction`) | ch6 | a *moving* magnet makes EMF (peak latch) | Coil, Magnet, Galvanometer |
| 8 | **The Dynamo** (new) | `LoadCrankGeneratorGadget` + `InductionCoil` | continuous motion → continuous EMF → light | Hand Crank, Bulb/LED, Oscilloscope |
| 9 | **Transformer** (finale, new) | `LoadMutualCouplerGadget` / `LoadTransformerGadget` | changing current in one coil induces EMF in another | Mutual Coupler, Voltmeter ×2 |

Optional bonus: `sun_power` (ch5) as "Light Without a Battery", placed between 5 and 6.
**Out of Chapter 1:** challenges 8–43 (combo permutations), the mechanics/optics/fluids gadgets (Chapter 2+), combat/operators (keep, but don't feature them).

New objective types needed for levels 6, 8 and 9 (add to `ObjectiveType`, evaluate in `ChallengeManager`):
- `FieldMapped`: compass or probe sampled at ≥N points near a dipole (the `FieldProbe` / `LoadCompassGadget` readouts already exist).
- `CrankEmfSustained`: crank-generator EMF ≥ X V held for T seconds (sustained, not latched).
- `MutualEmfThreshold`: secondary EMF from `LoadMutualCouplerGadget` ≥ X V.
- Change combo objectives to **simultaneous** (all true in the same poll) via a `requireSimultaneous` flag on `ChallengeDefinition`.

---

## 4. Prioritized build list

**P0 — blockers (can't ship or play-test without these)**
1. **Fix the LFS budget** (raise the GitHub LFS quota/data pack, or move the scene to a smaller split). Then push the pending `Faraday.unity` commit `914ddf8`.
2. **VR access to challenges:** add a "Challenges" chip to the EXPERIMENTS toolbelt tab and to `QhysicsPausePanel` that calls `ChallengeUi.ToggleList()`.
3. **Scrollable challenge list:** add `ScrollRect` + `RectMask2D` in `ChallengeUi.BuildListPanel()`, or page it with 8 per page like the toolbelt's `Chip_NextPage`.
4. **Chapter data model:** add `chapterId` and `order` to `ChallengeDefinition`, plus a `ChapterDefinition` (id, title, intro text, challenge ids, outro). Filter the list UI by chapter. Hide challenges 13–43 behind "Sandbox Challenges" (hide, don't delete).
5. **Isolate unbeatable targets:** move ch39–43 (≥2 W) and ch34–35 out of the main path, or retune them to per-circuit total power or the solar ceiling. Play-verify every Chapter 1 level on desktop and on Quest Link.
6. **Audio mixer:** track `Assets/Sounds/QhysicsMixer.mixer`, create a `Resources/QhysicsAudioMixerRef.asset` (or move the mixer into `Resources/`), and confirm the `[AudioRouter]` warning is gone.

**P1 — makes it a chapter**
7. Main menu v2 (`QhysicsMainMenu`): Continue / Chapter 1 / Sandbox / Settings / Quit, shown every boot (not gated by `EnteredOnce`).
8. Level 0 interactive tutorial (grab → snap → close loop → read meter) that replaces the text-only strip.
9. New objective types (`FieldMapped`, `CrankEmfSustained`, `MutualEmfThreshold`, `requireSimultaneous`) and levels 6, 8, 9.
10. Per-level intro card (1–2 lines of Faraday bench-notebook framing) and a Scientist mentor hint set per level.
11. Chapter complete screen: stars total, time, "facts you produced" recap, route back to menu / Sandbox.
12. Challenge SFX: objective tick (`CircuitClick`), complete (`Bell`), unlock (`ForceField`), UI press (`ButtonPress`), routed to the SFX group. Add chapter ambience.

**P2 — ship quality**
13. Smoke-test pass on every gadget used in Chapter 1 (spawn, grab XR + desktop, readout, New Run clears, F5/F9 restore).
14. Quest perf: Single Pass Instanced, profile `Faraday.unity` on Link and standalone. Hide or deactivate distant Giza/biology/chemistry content while Chapter 1 is active (never disable MountainScene). Bake lighting.
15. Comfort: vignette toggle, snap/smooth turn, seated height, dominant hand in `QhysicsSettingsPanel`.
16. Save breadboard layout per level (extend `LabSandboxSave` or checkpoint `CircuitLab`), plus a "Reset chapter progress" button.
17. Identity and build: `productName`/`companyName`/version, a Windows build profile, a recorded Quest Link session, an Android APK smoke test.
18. Text cleanup: fix mojibake in `QHYSICS_PLAY.md`, `ChallengeManager.cs`, `GizaComplex.cs` (`kawÃ¡b` match), `QhysicsGadgets.cs`.
19. Decide on the pending settings drift (`manifest.json` ai.assistant bump, OpenXR feature list, ProjectAuditor, TMP fallback atlas) and commit or revert it deliberately.


---

## 5. Status update — 2026-09-27 (runtime code, compile-verified, not yet Play-tested)

**Done (P0 2–6, P1 9 partially, P1 12):**
- **Chapters:** `Assets/Universe/Challenges/ChapterCatalog.cs` (code table `ChapterDefinition`: id, order, title, subtitle, ids, `sequentialUnlock`).
  - Chapter 1 "Faraday's Bench" (order 1), 10 levels: `bench_orientation` (new) → `first_light` → `make_and_break` → `current_control` → `double_trouble` → `spin_up` → `lines_of_force` (new) → `induction` → `the_dynamo` (new) → `transformer` (new finale).
  - Unlock: entry N unlocks when entry N−1 has ≥1 star, OR via the original prerequisite chain (kept).
  - "Sandbox / Extra" (order 99) = every other campaign id (old 13–43 + `sun_power`, `gear_up`, …). Hidden from Chapter 1, **not deleted**; original chain unlocks unchanged.
- **Beatability of every Chapter 1 level (the parts that exist):**

| Level | Objective | Why it is beatable |
|---|---|---|
| bench_orientation | any spawned `Gadget_*` root ≥ 1 | any toolbelt chip spawns one |
| first_light | 1 bulb lit | 1 battery + 1 bulb: I = 10 V / 1 kΩ = 10 mA, P = 0.1 W (> significance threshold) |
| make_and_break | switch closed + bulb lit | same loop + switch (series) |
| current_control | resistor in loop + bulb lit | 10 V / 1.47 kΩ = 6.8 mA, still lit |
| double_trouble | 2 bulbs lit | parallel: 10 mA each; series: 5 mA each (both above threshold) |
| spin_up | motor ≥ 120 RPM | motor 2 kΩ: I = 5 mA = baseCurrent ⇒ RPM = 600 × 5/5 = 600 ≥ 120 |
| lines_of_force | compass + horizontal \|B\| ≥ 150 µT | Earth 50 µT + dipole B ∝ 1/r³: halving distance is ×8, so a magnet within a few cm–dm clears 3× Earth (**threshold needs Play confirmation**) |
| induction | peak \|EMF\| ≥ 0.05 V | unchanged; existing latched-peak sampling every frame |
| the_dynamo | crank cranking + peak \|EMF\| ≥ 0.01 V held 2 s | rotating dipole near coil, sustained (hold timer resets if it drops) (**threshold needs Play confirmation**) |
| transformer | coupler coupled + latched \|Es\| ≥ 0.1 mV | Es = −M dIp/dt: M = 2 mH (MED), 10 mA switched in ~1 frame (11 ms) ⇒ dI/dt ≈ 0.9 A/s ⇒ Es ≈ 1.8 mV ≫ 0.1 mV |

- New objective types: `GadgetPresent` (9), `CompassFieldThreshold` (10), `CrankEmfThreshold` (11, sustained via `heldSeconds`), `MutualEmfThreshold` (12, latched peak) + live readouts. `ChallengeObjective.targetTag` added.
- **VR access:** EXPERIMENTS toolbelt tab "Challenges" chip; pause menu "Challenges" (and "Skills"); `C` key kept.
- **Scroll:** `ScrollRect` + `RectMask2D` + `ContentSizeFitter` (XR ray drag), right/left thumbstick Y while open, mouse wheel, UP/DOWN page buttons. Hotbar wheel, toolbelt page wheel and XR carry-pouch stick are suppressed while the list is open (`QhysicsUiScrollGate`).
- **Positioning bug fixed:** the old list/overlay/banner moved `_listPanel.root` (the whole QhysicsUI hierarchy). Now three separate canvases, each placed on its own.
- **Chapter complete** banner + SFX when the last Chapter 1 star lands.
- **Audio:** `Assets/Sounds/QhysicsMixer.mixer` now tracked; `Assets/Resources/QhysicsAudioMixerRef.asset` references it, so `AudioRouter.LoadMixer()` succeeds and the Settings volume routes through `MasterVolume` (fallback to `AudioListener.volume` if the ref is ever missing). `QhysicsSfx` generates procedural clips (objective tick, complete arpeggio, chapter fanfare, unlock, UI tick, denied, plus combat sounds) on pooled sources routed to the SFX group.

**Still open:** `requireSimultaneous` (objectives remain sticky), per-level intro cards, main menu v2, Play-verify every level on desktop + Quest Link, Quest perf items.
