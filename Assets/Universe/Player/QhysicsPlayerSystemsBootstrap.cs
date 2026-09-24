using UnityEngine;
using RealityEngine.UI;
using RealityEngine.XR;

namespace RealityEngine.Player
{
    /// <summary>
    /// Runtime Ensure for operator / carry inventory / training combat / XR grip pickup / XR pouch stick / plaza pickups + drones.
    /// Composes on existing desktop + UI bootstrap â€” no Faraday scene rewrite.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(136)]
    public sealed class QhysicsPlayerSystemsBootstrap : MonoBehaviour
    {
        public const string RootName = "QhysicsPlayerSystems";
        public const string WorldRootName = "QhysicsTrainingWorld";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoEnsure()
        {
            if (!Application.isPlaying)
                return;
            Ensure();
        }

        public static QhysicsPlayerSystemsBootstrap Ensure()
        {
            QhysicsPlayerSystemsBootstrap existing =
                UnityEngine.Object.FindFirstObjectByType<QhysicsPlayerSystemsBootstrap>(FindObjectsInactive.Include);
            if (existing == null)
            {
                GameObject host = GameObject.Find(RootName);
                if (host == null)
                    host = new GameObject(RootName);
                existing = host.GetComponent<QhysicsPlayerSystemsBootstrap>();
                if (existing == null)
                    existing = host.AddComponent<QhysicsPlayerSystemsBootstrap>();
            }
            existing.Build();
            return existing;
        }

        public void Build()
        {
            // Player stack
            PlayerCarryInventory.Ensure(transform);
            PlayerVitality.Ensure(transform);
            PlayerOperatorController.Ensure(transform);
            TrainingCombatController.Ensure(transform);
            XrWorldInteractController.Ensure(transform);
            XrCarryPouchController.Ensure(transform);

            // UI pieces (parent under QhysicsUI if present)
            Transform uiRoot = null;
            GameObject uiGo = GameObject.Find(QhysicsUiBootstrap.RootName);
            if (uiGo != null)
                uiRoot = uiGo.transform;
            QhysicsOperatorSelectPanel.Ensure(uiRoot != null ? uiRoot : transform);
            QhysicsCarryHud.Ensure(uiRoot != null ? uiRoot : transform);

            SpawnTrainingWorld();
        }

        void SpawnTrainingWorld()
        {
            GameObject world = GameObject.Find(WorldRootName);
            if (world != null)
                return;
            world = new GameObject(WorldRootName);

            Vector3 plaza = ResolvePlazaAnchor();
            // Pickups near induction / plaza
            QhysicsWorldPickup.Spawn(CarryItemId.HealthAmpoule, plaza + new Vector3(1.2f, 0.9f, 0.4f), world.transform);
            QhysicsWorldPickup.Spawn(CarryItemId.BatteryPack, plaza + new Vector3(1.6f, 0.9f, -0.2f), world.transform);
            QhysicsWorldPickup.Spawn(CarryItemId.ProbeTip, plaza + new Vector3(0.8f, 0.9f, -0.6f), world.transform);
            QhysicsWorldPickup.Spawn(CarryItemId.TrainingBaton, plaza + new Vector3(2.0f, 0.95f, 0.2f), world.transform);
            QhysicsWorldPickup.Spawn(CarryItemId.ShieldCell, plaza + new Vector3(1.0f, 0.9f, 0.9f), world.transform);

            // Training drones east of plaza
            TrainingDrone.Spawn(plaza + new Vector3(3.5f, 1.4f, 1.5f), world.transform);
            TrainingDrone.Spawn(plaza + new Vector3(4.2f, 1.6f, -1.0f), world.transform);
        }

        static Vector3 ResolvePlazaAnchor()
        {
            // Prefer current XR Origin (already parked on plaza by LabPlayerSpawn).
            GameObject originGo = GameObject.Find(LabPlayerSpawn.OriginName);
            if (originGo != null)
                return originGo.transform.position;

            Transform plaza = FindNamedContains("LabPlaza");
            if (plaza != null)
                return plaza.position + Vector3.up * 0.1f;

            return new Vector3(0f, 0.1f, 0f);
        }

        static Transform FindNamedContains(string token)
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            string t = token.ToLowerInvariant();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name.ToLowerInvariant().Contains(t))
                    return all[i];
            }
            return null;
        }
    }
}
