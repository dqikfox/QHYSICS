using UnityEngine;
using UnityEngine.XR;
using RealityEngine.UI;

// FORGE_SLICE 2026-09-24 hip-pouch XR
namespace RealityEngine.Player
{
    /// <summary>
    /// XR hip-pouch carry controls when an XR display is running.
    /// Completes grip-pickup loop without companion keyboard:
    /// thumbstick L/R cycle slots, click use/equip, flick-down drop.
    /// Does not steal grip (pickup) or trigger (baton swing).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(142)]
    public sealed class XrCarryPouchController : MonoBehaviour
    {
        public const string RootName = "XrCarryPouchController";

        public static XrCarryPouchController Instance { get; private set; }

        [SerializeField] float axisCycleThreshold = 0.72f;
        [SerializeField] float axisDropThreshold = -0.72f;
        [SerializeField] float axisDeadzone = 0.35f;

        bool _prevLeftClick;
        bool _prevRightClick;
        bool _prevLeftCycleLatch;
        bool _prevRightCycleLatch;
        bool _prevLeftDropLatch;
        bool _prevRightDropLatch;
        QhysicsPausePanel _pauseCached;
        float _nextPauseScan;

        public static XrCarryPouchController Ensure(Transform parent = null)
        {
            if (Instance != null)
                return Instance;
            var found = UnityEngine.Object.FindFirstObjectByType<XrCarryPouchController>(FindObjectsInactive.Include);
            if (found != null)
            {
                Instance = found;
                return found;
            }
            var go = new GameObject(RootName);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.AddComponent<XrCarryPouchController>();
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
            if (!DesktopPlayerController.IsXrDisplayRunning())
            {
                ResetEdges();
                return;
            }
            if (IsPauseOrOperatorOpen() || QhysicsUiScrollGate.IsCaptured)
            {
                // Challenge list / skills panel use the thumbstick to scroll while open.
                ResetEdges();
                return;
            }

            var carry = PlayerCarryInventory.Instance;
            if (carry == null)
                return;

            // Prefer right hand axis, then left â€” either may drive pouch.
            bool used = false;
            used |= PollHand(XRNode.RightHand, carry, ref _prevRightClick, ref _prevRightCycleLatch, ref _prevRightDropLatch);
            if (!used)
                PollHand(XRNode.LeftHand, carry, ref _prevLeftClick, ref _prevLeftCycleLatch, ref _prevLeftDropLatch);
        }

        bool PollHand(XRNode node, PlayerCarryInventory carry,
            ref bool prevClick, ref bool prevCycleLatch, ref bool prevDropLatch)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
            {
                prevClick = false;
                prevCycleLatch = false;
                prevDropLatch = false;
                return false;
            }

            bool acted = false;

            // Thumbstick click = use / equip selected (U on desktop).
            bool click = false;
            if (device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool clk) && clk)
                click = true;
            if (click && !prevClick)
            {
                carry.TryUseOrEquipSelected();
                acted = true;
            }
            prevClick = click;

            Vector2 axis = Vector2.zero;
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out axis);

            // Horizontal flick = cycle carry slots ([ ] on desktop).
            bool cycleLatch = Mathf.Abs(axis.x) >= axisCycleThreshold && Mathf.Abs(axis.x) >= Mathf.Abs(axis.y);
            if (cycleLatch && !prevCycleLatch)
            {
                carry.SelectDelta(axis.x > 0f ? 1 : -1);
                acted = true;
            }
            if (Mathf.Abs(axis.x) < axisDeadzone)
                prevCycleLatch = false;
            else
                prevCycleLatch = cycleLatch;

            // Down flick = drop selected into world (X on desktop).
            bool dropLatch = axis.y <= axisDropThreshold && Mathf.Abs(axis.y) > Mathf.Abs(axis.x);
            if (dropLatch && !prevDropLatch)
            {
                if (carry.TryDropSelected(out CarryItemId id, out Vector3 pos))
                {
                    // Prefer drop slightly ahead of that hand if pose known.
                    if (TryHandPose(node, out Vector3 handPos, out Quaternion handRot))
                        pos = handPos + handRot * Vector3.forward * 0.35f + Vector3.up * 0.05f;
                    QhysicsWorldPickup.Spawn(id, pos);
                }
                acted = true;
            }
            if (axis.y > -axisDeadzone)
                prevDropLatch = false;
            else
                prevDropLatch = dropLatch;

            return acted;
        }

        static bool TryHandPose(XRNode node, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
                return false;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out pos))
                return false;
            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
                return false;
            return true;
        }

        bool IsPauseOrOperatorOpen()
        {
            if (_pauseCached == null && Time.unscaledTime >= _nextPauseScan)
            {
                _nextPauseScan = Time.unscaledTime + 0.5f;
                _pauseCached = UnityEngine.Object.FindAnyObjectByType<QhysicsPausePanel>(FindObjectsInactive.Include);
            }
            if (_pauseCached != null && _pauseCached.IsOpen)
                return true;
            // Avoid hard dependency on OperatorSelectPanel type during partial compiles.
            GameObject opGo = GameObject.Find("QhysicsOperatorSelectPanel");
            if (opGo == null || !opGo.activeInHierarchy)
                return false;
            var behaviours = opGo.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null) continue;
                var p = b.GetType().GetProperty("IsOpen");
                if (p != null && p.PropertyType == typeof(bool) && (bool)p.GetValue(b))
                    return true;
            }
            return false;
        }

        void ResetEdges()
        {
            _prevLeftClick = false;
            _prevRightClick = false;
            _prevLeftCycleLatch = false;
            _prevRightCycleLatch = false;
            _prevLeftDropLatch = false;
            _prevRightDropLatch = false;
        }
    }
}
