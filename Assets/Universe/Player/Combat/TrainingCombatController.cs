using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Training System melee: LMB / Fire1 while Training Baton equipped.
    /// Hits <see cref="IDamageable"/> in a short forward cone (desktop + XR-safe).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(140)]
    public sealed class TrainingCombatController : MonoBehaviour
    {
        public const string RootName = "TrainingCombatController";

        public static TrainingCombatController Instance { get; private set; }

        [SerializeField] float range = 2.2f;
        [SerializeField] float radius = 0.35f;
        [SerializeField] float damage = 22f;
        [SerializeField] float cooldown = 0.35f;

        float _nextSwing;
        readonly Collider[] _hits = new Collider[16];

        public static TrainingCombatController Ensure(Transform parent = null)
        {
            if (Instance != null)
                return Instance;
            var found = Object.FindFirstObjectByType<TrainingCombatController>(FindObjectsInactive.Include);
            if (found != null)
            {
                Instance = found;
                return found;
            }
            var go = new GameObject(RootName);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.AddComponent<TrainingCombatController>();
        }

        void OnEnable() => Instance = this;
        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;
            var pause = Object.FindFirstObjectByType<RealityEngine.UI.QhysicsPausePanel>(FindObjectsInactive.Include);
            if (pause != null && pause.IsOpen)
                return;

            var carry = PlayerCarryInventory.Instance;
            if (carry == null || carry.EquippedItem != CarryItemId.TrainingBaton)
                return;

            if (!WasAttackPressed())
                return;
            if (Time.time < _nextSwing)
                return;
            _nextSwing = Time.time + cooldown;
            Swing();
        }

        void Swing()
        {
            Transform cam = ResolveAim();
            if (cam == null)
                return;

            Vector3 origin = cam.position;
            Vector3 dir = cam.forward;
            int n = Physics.OverlapSphereNonAlloc(origin + dir * (range * 0.45f), radius + range * 0.25f, _hits,
                ~0, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            IDamageable bestTarget = null;
            Vector3 hitPt = origin + dir * range;
            Vector3 hitN = -dir;

            for (int i = 0; i < n; i++)
            {
                Collider c = _hits[i];
                if (c == null)
                    continue;
                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive)
                    continue;
                // Don't hit self
                if (c.GetComponentInParent<PlayerVitality>() != null)
                    continue;
                Vector3 pt = c.ClosestPoint(origin + dir * 0.5f);
                float along = Vector3.Dot(pt - origin, dir);
                if (along < 0.2f || along > range)
                    continue;
                float lateral = Vector3.Cross(dir, pt - origin).magnitude;
                if (lateral > radius + 0.4f)
                    continue;
                float score = along + lateral * 0.5f;
                if (score < best)
                {
                    best = score;
                    bestTarget = dmg;
                    hitPt = pt;
                    hitN = (origin - pt).normalized;
                }
            }

            if (bestTarget != null)
                bestTarget.ApplyDamage(damage, hitPt, hitN);
        }

        static Transform ResolveAim()
        {
            var desktop = DesktopPlayerController.Instance;
            if (desktop != null && desktop.MainCamera != null)
                return desktop.MainCamera;
            if (Camera.main != null)
                return Camera.main.transform;
            return null;
        }

        static bool WasAttackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0) || Input.GetButtonDown("Fire1"))
                return true;
#endif
            return false;
        }
    }
}
