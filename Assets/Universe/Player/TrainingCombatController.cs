using UnityEngine;
using UnityEngine.XR;
using RealityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Training System melee: desktop LMB / Fire1, or XR controller trigger/activate,
    /// while Training Baton is equipped. Same damage cone for both paths.
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
        QhysicsPausePanel _pauseCached;
        float _nextPauseScan;

        // XR trigger edge state (null-safe when no headset).
        bool _prevLeftTrig;
        bool _prevRightTrig;
        XRNode _lastTrigNode = XRNode.RightHand;

        public static TrainingCombatController Ensure(Transform parent = null)
        {
            if (Instance != null)
                return Instance;
            var found = UnityEngine.Object.FindFirstObjectByType<TrainingCombatController>(FindObjectsInactive.Include);
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
            if (IsPauseOpen())
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

        /// <summary>Public entry so XR activate listeners can share the desktop damage path.</summary>
        public bool TrySwingFromExternal()
        {
            var carry = PlayerCarryInventory.Instance;
            if (carry == null || carry.EquippedItem != CarryItemId.TrainingBaton)
                return false;
            if (IsPauseOpen())
                return false;
            if (Time.time < _nextSwing)
                return false;
            _nextSwing = Time.time + cooldown;
            Swing();
            return true;
        }

        bool IsPauseOpen()
        {
            if (_pauseCached == null && Time.unscaledTime >= _nextPauseScan)
            {
                _nextPauseScan = Time.unscaledTime + 0.5f;
                _pauseCached = UnityEngine.Object.FindFirstObjectByType<QhysicsPausePanel>(FindObjectsInactive.Include);
            }
            return _pauseCached != null && _pauseCached.IsOpen;
        }

        void Swing()
        {
            if (!TryResolveAim(out Vector3 origin, out Vector3 dir))
                return;

            int n = UnityEngine.Physics.OverlapSphereNonAlloc(origin + dir * (range * 0.45f), radius + range * 0.25f, _hits,
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

        bool TryResolveAim(out Vector3 origin, out Vector3 dir)
        {
            origin = Vector3.zero;
            dir = Vector3.forward;

            // Prefer the hand that just pressed trigger when XR is running.
            if (DesktopPlayerController.IsXrDisplayRunning())
            {
                if (TryControllerAim(_lastTrigNode, out origin, out dir))
                    return true;
                if (TryControllerAim(XRNode.RightHand, out origin, out dir))
                    return true;
                if (TryControllerAim(XRNode.LeftHand, out origin, out dir))
                    return true;
            }

            Transform cam = null;
            var desktop = DesktopPlayerController.Instance;
            if (desktop != null && desktop.MainCamera != null)
                cam = desktop.MainCamera;
            else if (Camera.main != null)
                cam = Camera.main.transform;
            if (cam == null)
                return false;
            origin = cam.position;
            dir = cam.forward;
            return true;
        }

        static bool TryControllerAim(XRNode node, out Vector3 origin, out Vector3 dir)
        {
            origin = Vector3.zero;
            dir = Vector3.forward;
            UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
                return false;
            if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 pos))
                return false;
            if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rot))
                return false;
            origin = pos;
            dir = rot * Vector3.forward;
            return dir.sqrMagnitude > 1e-6f;
        }

        bool WasAttackPressed()
        {
            // Desktop LMB / Fire1
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0) || Input.GetButtonDown("Fire1"))
                return true;
#endif
            // XR: either controller trigger / activate (edge-detect). Null-safe with no headset.
            if (WasXrTriggerPressed(XRNode.LeftHand, ref _prevLeftTrig))
            {
                _lastTrigNode = XRNode.LeftHand;
                return true;
            }
            if (WasXrTriggerPressed(XRNode.RightHand, ref _prevRightTrig))
            {
                _lastTrigNode = XRNode.RightHand;
                return true;
            }
            return false;
        }

        static bool WasXrTriggerPressed(XRNode node, ref bool prevDown)
        {
            bool down = false;
            UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid)
            {
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool btn) && btn)
                    down = true;
                else if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float axis) && axis > 0.72f)
                    down = true;
                // Some OpenXR profiles expose activate as primaryButton on grip-side — keep soft.
            }
            bool pressed = down && !prevDown;
            prevDown = down;
            return pressed;
        }
    }
}
