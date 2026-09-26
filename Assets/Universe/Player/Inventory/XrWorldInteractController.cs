using UnityEngine;
using UnityEngine.XR;
using RealityEngine.UI;
using RealityEngine.XR;

namespace RealityEngine.Player
{
    /// <summary>
    /// XR near-hand / short-ray interact for plaza <see cref="IQhysicsInteractable"/> pickups.
    /// Grip (not trigger) so Training Baton swings stay on trigger. No-op when XR display is off.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(141)] // FORGE_POUCH_REFRESH
    public sealed class XrWorldInteractController : MonoBehaviour
    {
        public const string RootName = "XrWorldInteractController";

        public static XrWorldInteractController Instance { get; private set; }

        [SerializeField] float rayDistance = 1.5f;
        [SerializeField] float overlapRadius = 0.15f;
        [SerializeField] LayerMask hitMask = ~0;

        readonly Collider[] _overlap = new Collider[24];
        IQhysicsInteractable _hover;
        bool _prevLeftGrip;
        bool _prevRightGrip;
        QhysicsPausePanel _pauseCached;
        float _nextPauseScan;
        GameObject _actor;

        public static XrWorldInteractController Ensure(Transform parent = null)
        {
            if (Instance != null)
                return Instance;
            var found = UnityEngine.Object.FindFirstObjectByType<XrWorldInteractController>(FindObjectsInactive.Include);
            if (found != null)
            {
                Instance = found;
                return found;
            }
            var go = new GameObject(RootName);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.AddComponent<XrWorldInteractController>();
        }

        void OnEnable() => Instance = this;
        void OnDisable()
        {
            ClearHover();
            if (Instance == this)
                Instance = null;
        }

        void Awake()
        {
            // Never raycast own head/self layer (PlayerSelf = 31).
            hitMask &= ~(1 << QhysicsDesktopBootstrap.PlayerSelfLayer);
            ResolveActor();
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            if (!DesktopPlayerController.IsXrDisplayRunning())
            {
                ClearHover();
                _prevLeftGrip = false;
                _prevRightGrip = false;
                return;
            }

            if (IsPauseOpen())
            {
                ClearHover();
                return;
            }

            ResolveActor();

            IQhysicsInteractable best = null;
            float bestScore = float.MaxValue;
            EvaluateHand(XRNode.LeftHand, ref best, ref bestScore);
            EvaluateHand(XRNode.RightHand, ref best, ref bestScore);
            SetHoverTarget(best);

            bool leftGrip = WasGripPressed(XRNode.LeftHand, ref _prevLeftGrip);
            bool rightGrip = WasGripPressed(XRNode.RightHand, ref _prevRightGrip);
            if ((leftGrip || rightGrip) && _hover != null && _actor != null && _hover.CanInteract(_actor))
            {
                _hover.Interact(_actor);
                ClearHover();
            }
        }

        void ResolveActor()
        {
            if (_actor != null)
                return;
            GameObject originGo = GameObject.Find(LabPlayerSpawn.OriginName);
            if (originGo != null)
            {
                _actor = originGo;
                return;
            }
            if (transform.root != null)
                _actor = transform.root.gameObject;
            else
                _actor = gameObject;
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

        void EvaluateHand(XRNode node, ref IQhysicsInteractable best, ref float bestScore)
        {
            if (!TryControllerPose(node, out Vector3 pos, out Quaternion rot))
                return;
            Vector3 forward = rot * Vector3.forward;
            if (forward.sqrMagnitude < 1e-6f)
                return;
            forward.Normalize();

            // Short forward ray
            if (UnityEngine.Physics.Raycast(pos, forward, out RaycastHit hit, rayDistance, hitMask, QueryTriggerInteraction.Ignore))
                ConsiderCollider(hit.collider, pos, ref best, ref bestScore);

            // Near-hand overlap sphere
            int n = UnityEngine.Physics.OverlapSphereNonAlloc(pos, overlapRadius, _overlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                ConsiderCollider(_overlap[i], pos, ref best, ref bestScore);
        }

        void ConsiderCollider(Collider col, Vector3 from, ref IQhysicsInteractable best, ref float bestScore)
        {
            if (col == null)
                return;
            var ix = col.GetComponentInParent<IQhysicsInteractable>();
            if (ix == null)
                return;
            if (_actor != null && !ix.CanInteract(_actor))
                return;
            // Prefer MonoBehaviour distance when available
            float d = float.MaxValue;
            var mb = ix as MonoBehaviour;
            if (mb != null)
                d = (mb.transform.position - from).sqrMagnitude;
            else
                d = (col.transform.position - from).sqrMagnitude;
            if (d < bestScore)
            {
                bestScore = d;
                best = ix;
            }
        }

        void SetHoverTarget(IQhysicsInteractable next)
        {
            if (next == _hover)
                return;
            if (_hover != null)
                _hover.SetHover(false);
            _hover = next;
            if (_hover != null)
                _hover.SetHover(true);
        }

        void ClearHover()
        {
            if (_hover == null)
                return;
            _hover.SetHover(false);
            _hover = null;
        }

        static bool TryControllerPose(XRNode node, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
                return false;
            // World pose (raw device pose is tracking space; wrong once the rig is at the plaza).
            return XrTrackingSpace.TryGetWorldPose(node, out pos, out rot);
        }

        /// <summary>
        /// Grip edge: gripButton, else grip axis &gt; 0.75, else primaryButton.
        /// Trigger stays free for <see cref="TrainingCombatController"/> baton swings.
        /// </summary>
        static bool WasGripPressed(XRNode node, ref bool prevDown)
        {
            bool down = false;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripBtn) && gripBtn)
                    down = true;
                else if (device.TryGetFeatureValue(CommonUsages.grip, out float gripAxis) && gripAxis > 0.75f)
                    down = true;
                else if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary) && primary)
                    down = true;
            }
            bool pressed = down && !prevDown;
            prevDown = down;
            return pressed;
        }
    }
}