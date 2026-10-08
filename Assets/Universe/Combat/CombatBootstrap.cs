using System.Collections;
using UnityEngine;
using RealityEngine.Audio;
using RealityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Combat
{
    /// <summary>
    /// Runtime bootstrap for the combat layer (no scene edits): physics hands, spells, skills, HUD,
    /// hurtbox, enemy director and the arena near the plaza. Debug/spawn keys: F2 dagger, F3 sword,
    /// F4 spear, F6 mace, F7 shield, F8 enemy (Shift+F8 brute, Ctrl+F8 cycles Duelist / Caster / Warden), F10 dummy,
    /// L arena teleport (Shift+L start/stop waves, Alt+L cycle wave tier), F11 ground slam, F12 blade recall (once unlocked in Skills).
    /// </summary>
    [DefaultExecutionOrder(151)]
    public sealed class CombatBootstrap : MonoBehaviour
    {
        public static CombatBootstrap Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("QhysicsCombatBootstrap");
            Instance = go.AddComponent<CombatBootstrap>();
        }

        void Awake()
        {
            Instance = this;
            PhysicsHands.Ensure();
            SpellSystem.Ensure();
            CombatAbilities.Ensure();
            CombatSfxDirector.Ensure();
            PlayerHurtbox.Ensure();
            EnemyDirector.Ensure();
            SkillSystem.Hook();
            SkillsPanel.Ensure();
            CombatHud.Ensure();
            QhysicsPausePanel.OpenSkillsHook = SkillsPanel.Open;
            StartCoroutine(BuildArenaLater());
        }

        IEnumerator BuildArenaLater()
        {
            // Let LabPlayerSpawn park the player on the plaza first.
            yield return new WaitForSeconds(2.5f);
            var origin = GameObject.Find("XR Origin");
            Vector3 plaza = origin != null ? origin.transform.position : Vector3.zero;
            CombatArena.Build(plaza);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null || CombatInputGate.Blocked) return;
            if (kb.f2Key.wasPressedThisFrame) SpawnWeapon(WeaponKind.Dagger);
            if (kb.f3Key.wasPressedThisFrame) SpawnWeapon(WeaponKind.Sword);
            if (kb.f4Key.wasPressedThisFrame) SpawnWeapon(WeaponKind.Spear);
            if (kb.f6Key.wasPressedThisFrame) SpawnWeapon(WeaponKind.Mace);
            if (kb.f7Key.wasPressedThisFrame) SpawnWeapon(WeaponKind.Shield);
            if (kb.f8Key.wasPressedThisFrame)
            {
                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
                    CombatEnemy.NextSpawnOverride = EnemyType.Brute;
                else if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)
                {
                    _ctrlCycle = (_ctrlCycle + 1) % 3;
                    CombatEnemy.NextSpawnOverride = _ctrlCycle == 0 ? EnemyType.Duelist : _ctrlCycle == 1 ? EnemyType.Caster : EnemyType.Warden;
                }
                if (!EnemyDirector.Ensure().SpawnInFrontOfPlayer())
                    CombatEnemy.NextSpawnOverride = null;
            }
            if (kb.f10Key.wasPressedThisFrame) SpawnDummy();
            if (kb.lKey.wasPressedThisFrame)
            {
                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) ToggleWaves();
                else if (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed) CycleArenaTier();
                else ToggleArena();
            }
#endif
        }

        /// <summary>Spawns a weapon in front of the player; on desktop it goes straight into the hand.</summary>
        public static void SpawnWeapon(WeaponKind kind)
        {
            var cam = CombatInputGate.ViewCamera();
            if (cam == null) return;
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 p = cam.transform.position + fwd * 0.6f + Vector3.down * 0.3f;
            var w = WeaponFactory.Spawn(kind, p, Quaternion.LookRotation(fwd) * Quaternion.Euler(-60f, 0f, 0f));
            if (!CombatInputGate.IsXr)
                PhysicsHands.Ensure().DesktopGrab(w);
            QhysicsSfx.Play2D(SfxId.UiTick, 0.5f);
        }

        public static void SpawnEnemy() => EnemyDirector.Ensure().SpawnInFrontOfPlayer();

        static int _ctrlCycle = -1;

        /// <summary>Toolbelt Duelist / Caster / Warden chips (same 3-alive cap as Enemy).</summary>
        public static void SpawnEnemyOfType(EnemyType type)
        {
            CombatEnemy.NextSpawnOverride = type;
            if (!EnemyDirector.Ensure().SpawnInFrontOfPlayer())
                CombatEnemy.NextSpawnOverride = null;
        }

        public static void SpawnDummy()
        {
            var cam = CombatInputGate.ViewCamera();
            if (cam == null) return;
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 p = cam.transform.position + fwd * 2.2f;
            if (!CombatArena.GroundAt(p, out Vector3 g)) g = new Vector3(p.x, cam.transform.position.y - 1.6f, p.z);
            TrainingDummy.Spawn(g + Vector3.up * 0.02f, Quaternion.LookRotation(-fwd));
        }

        static CombatArena EnsureArena()
        {
            if (CombatArena.Instance == null)
            {
                var origin = GameObject.Find("XR Origin");
                CombatArena.Build(origin != null ? origin.transform.position : Vector3.zero);
            }
            return CombatArena.Instance;
        }

        /// <summary>Toolbelt "Waves" / Shift+L: start (teleporting into the arena if needed) or stop arena waves.</summary>
        public static void ToggleWaves()
        {
            var arena = EnsureArena();
            if (arena == null) return;
            var waves = ArenaWaves.Ensure(arena);
            if (waves == null) return;
            if (!ArenaWaves.Active && !arena.PlayerInside())
                arena.TogglePlayerTeleport();
            waves.Toggle();
        }

        /// <summary>Toolbelt "Tier" / Alt+L: cycle Recruit / Veteran / Champion / Legend.</summary>
        public static void CycleArenaTier()
        {
            var waves = ArenaWaves.Ensure(EnsureArena());
            if (waves != null) waves.CycleTier();
        }

        public static void ToggleArena()
        {
            if (CombatArena.Instance == null)
            {
                var origin = GameObject.Find("XR Origin");
                CombatArena.Build(origin != null ? origin.transform.position : Vector3.zero);
            }
            CombatArena.Instance.TogglePlayerTeleport();
        }
    }
}
