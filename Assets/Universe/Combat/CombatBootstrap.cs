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
    /// F4 spear, F6 mace, F7 shield, F8 enemy, F10 dummy, L arena teleport.
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
            if (kb.f8Key.wasPressedThisFrame) SpawnEnemy();
            if (kb.f10Key.wasPressedThisFrame) SpawnDummy();
            if (kb.lKey.wasPressedThisFrame) ToggleArena();
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
