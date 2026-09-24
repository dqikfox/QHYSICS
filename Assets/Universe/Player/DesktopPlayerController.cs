using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using RealityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Desktop keyboard/mouse locomotion for XR Origin when no headset / Link is active.
    /// Moves Origin (not camera). Leaves Quest Link XR locomotion alone when XR display is running.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)]
    public sealed class DesktopPlayerController : MonoBehaviour
    {
        public const string HostName = "QhysicsDesktopPlayer";

        // Plaza locomotion feel (desktop only; XR uses SmoothMovement + snap).
        [SerializeField] float walkSpeed = 3.4f;
        [SerializeField] float sprintSpeed = 6.8f;
        [SerializeField] float lookSensitivity = 1.55f;
        [SerializeField] float jumpSpeed = 5.2f;
        [SerializeField] float crouchHeight = 1.0f;
        [SerializeField] float standHeight = 1.72f;
        [SerializeField] float gravity = -22f;
        [SerializeField] float desktopEyeHeight = 1.65f;
        [SerializeField] float crouchEyeHeight = 0.95f;
        [SerializeField] float minPitch = -80f;
        [SerializeField] float maxPitch = 80f;
        [SerializeField] float acceleration = 12f;          // polished acceleration
        [SerializeField] float deceleration = 18f;
        const float MinLookSensitivity = 0.35f;
        const float MaxLookSensitivity = 3.5f;
        const float DesktopFovDefault = 75f;

        Transform _origin;
        Transform _cameraOffset;
        Transform _mainCamera;
        CharacterController _cc;
        float _pitch;
        float _yaw;
        float _vertVel;
        bool _crouching;
        bool _desktopActive;
        Vector3 _currentVelocity;   // for acceleration polish
        bool _cursorOwned;
        float _baseCcHeight = 1.72f;
        Vector3 _baseCcCenter;

        public bool IsDesktopActive => _desktopActive;
        public Transform Origin => _origin;
        public Transform MainCamera => _mainCamera;
        public static DesktopPlayerController Instance { get; private set; }

        public static bool IsXrDisplayRunning()
        {
            // Only a RUNNING XR display counts. OpenXR often stays "loaded"/isDeviceActive
            // with no headset — that must NOT disable desktop WASD.
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            for (int i = 0; i < displays.Count; i++)
            {
                if (displays[i] != null && displays[i].running)
                    return true;
            }
            return false;
        }

        public void Bind(Transform origin)
        {
            _origin = origin;
            if (_origin == null)
                return;

            // Never locomote a monument / Giza child if a bad reference was passed.
            if (LabPlayerSpawnCompat.IsMonumentTransform(_origin))
            {
                Debug.LogError("DesktopPlayerController: refused to Bind monument '" + _origin.name + "'; re-finding XR Origin.");
                _origin = FindOrigin();
                if (_origin == null || LabPlayerSpawnCompat.IsMonumentTransform(_origin))
                    return;
            }

            LabPlayerSpawnCompat.EnsurePlayerNotUnderMonument(_origin);
            LabPlayerSpawnCompat.StripMonumentChildren(_origin);

            _cc = _origin.GetComponent<CharacterController>();
            if (_cc == null)
                _cc = _origin.gameObject.AddComponent<CharacterController>();
            if (_cc != null)
            {
                LabPlayerSpawnCompat.ApplyCharacterControllerProfile(_cc);
                if (!_cc.enabled)
                    _cc.enabled = true;
                _baseCcHeight = _cc.height > 0.1f ? _cc.height : LabPlayerSpawnCompat.CcHeight;
                _baseCcCenter = _cc.center;
                standHeight = _baseCcHeight;
            }
            _cameraOffset = _origin.Find(LabPlayerSpawnCompat.CameraOffsetName);
            if (_cameraOffset == null)
            {
                var xr = _origin.GetComponent<XROrigin>();
                if (xr != null && xr.CameraFloorOffsetObject != null)
                    _cameraOffset = xr.CameraFloorOffsetObject.transform;
            }
            _mainCamera = null;
            if (_cameraOffset != null)
            {
                Transform camTf = _cameraOffset.Find(LabPlayerSpawnCompat.MainCameraName);
                if (camTf != null)
                    _mainCamera = camTf;
                else
                {
                    var cam = _cameraOffset.GetComponentInChildren<Camera>(true);
                    if (cam != null)
                        _mainCamera = cam.transform;
                }
            }
            // Never fall back to BuildingBlock/OVR CenterEyeAnchor via Camera.main.
            if (_mainCamera == null)
            {
                var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i] == null)
                        continue;
                    if (cams[i].transform.IsChildOf(_origin))
                    {
                        _mainCamera = cams[i].transform;
                        break;
                    }
                }
            }

            if (_origin != null)
                _yaw = _origin.eulerAngles.y;
            if (_mainCamera != null)
            {
                Vector3 e = _mainCamera.localEulerAngles;
                _pitch = e.x > 180f ? e.x - 360f : e.x;
            }

            ApplyDesktopEyeHeight(force: true);
            SoftClampDesktopFov();
        }

        /// <summary>Desktop comfort FOV only — leave XR / extreme artistic FOVs alone when display is running.</summary>
        void SoftClampDesktopFov()
        {
            if (_mainCamera == null || IsXrDisplayRunning())
                return;
            var cam = _mainCamera.GetComponent<Camera>();
            if (cam == null)
                return;
            // Note: desktop default ~75; clamp only if wildly off (broken scene / prefab).
            if (cam.fieldOfView < 50f || cam.fieldOfView > 100f)
                cam.fieldOfView = DesktopFovDefault;
        }

        /// <summary>Desktop floor-relative eye height on Camera Offset (~1.6-1.7m). XR Floor leaves offset at 0.</summary>
        public void ApplyDesktopEyeHeight(bool force = false)
        {
            if (_origin == null)
                return;
            bool xr = IsXrDisplayRunning();
            if (_cameraOffset == null)
            {
                _cameraOffset = _origin.Find(LabPlayerSpawnCompat.CameraOffsetName);
                var xrOrigin = _origin.GetComponent<XROrigin>();
                if (_cameraOffset == null && xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null)
                    _cameraOffset = xrOrigin.CameraFloorOffsetObject.transform;
            }
            if (_cameraOffset == null)
                return;

            var xrComp = _origin.GetComponent<XROrigin>();
            if (xr)
            {
                // Floor tracking: do not invent a standing offset under the HMD.
                if (xrComp != null && xrComp.RequestedTrackingOriginMode != XROrigin.TrackingOriginMode.Floor)
                    xrComp.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                if (force || Mathf.Abs(_cameraOffset.localPosition.y) > 0.01f)
                {
                    Vector3 lp = _cameraOffset.localPosition;
                    // Only zero when we previously raised it for desktop; leave non-zero XR floors alone if already ~0.
                    if (Mathf.Abs(lp.y - desktopEyeHeight) < 0.05f || force)
                    {
                        lp.y = 0f;
                        _cameraOffset.localPosition = lp;
                    }
                }
                if (xrComp != null && Mathf.Abs(xrComp.CameraYOffset) > 0.01f && force)
                    xrComp.CameraYOffset = 0f;
                return;
            }

            // Desktop: Camera Offset Y = stand/crouch eye height so Main Camera ducks with Ctrl.
            float targetEye = _crouching ? crouchEyeHeight : desktopEyeHeight;
            Vector3 dlp = _cameraOffset.localPosition;
            if (force)
                dlp.y = targetEye;
            else
                dlp.y = Mathf.Lerp(dlp.y, targetEye, Time.deltaTime * 12f);
            _cameraOffset.localPosition = dlp;
            if (_mainCamera != null)
            {
                Vector3 camLp = _mainCamera.localPosition;
                if (Mathf.Abs(camLp.y) > 0.05f)
                {
                    camLp.y = 0f;
                    _mainCamera.localPosition = camLp;
                }
            }
            if (xrComp != null)
            {
                if (xrComp.RequestedTrackingOriginMode != XROrigin.TrackingOriginMode.Floor)
                    xrComp.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                // Device-mode leftover Y offset fights desktop Camera Offset height.
                if (Mathf.Abs(xrComp.CameraYOffset) > 0.01f)
                    xrComp.CameraYOffset = 0f;
            }
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
            ReleaseCursor();
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;
            if (_origin == null)
                Bind(FindOrigin());
            if (_origin == null)
                return;

            bool xr = IsXrDisplayRunning();
            _desktopActive = !xr;
            if (!_desktopActive)
            {
                ReleaseCursor();
                return;
            }

            // Re-assert every frame: origin must stay the XR Origin, never a pyramid.
            if (_origin == null || LabPlayerSpawnCompat.IsMonumentTransform(_origin)
                || (_origin.parent != null && LabPlayerSpawnCompat.IsMonumentTransform(_origin.parent)))
            {
                Bind(FindOrigin());
                if (_origin == null)
                    return;
            }
            LabPlayerSpawnCompat.EnsurePlayerNotUnderMonument(_origin);
            LabPlayerSpawnCompat.EnsureDesktopViewAuthority(_origin, _mainCamera);

            bool pauseOpen = IsPauseOpen();
            if (pauseOpen)
            {
                ReleaseCursor();
                return;
            }

            EnsureCursorLocked();
            if (ReadRecalibrateHeight())
            {
                LabPlayerSpawnCompat.EnsurePlayerNotUnderMonument(_origin);
                ApplyDesktopEyeHeight(force: true);
                RealityEngine.XR.LabPlayerSpawn.RecalibratePlayerHeight(_origin, _origin.GetComponent<XROrigin>(), true);
            }
            else
                ApplyDesktopEyeHeight(force: false);
            ApplyLook();
            ApplyMove();
        }

        static Transform FindOrigin()
        {
            GameObject go = GameObject.Find(LabPlayerSpawnCompat.OriginName);
            if (go != null)
                return go.transform;
            var origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < origins.Length; i++)
            {
                if (origins[i] == null)
                    continue;
                string n = origins[i].gameObject.name;
                if (n != null && (n.IndexOf("OVR", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Oculus", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                return origins[i].transform;
            }
            return null;
        }

        static bool IsPauseOpen()
        {
            var pause = Object.FindFirstObjectByType<QhysicsPausePanel>(FindObjectsInactive.Include);
            return pause != null && pause.IsOpen;
        }

        void EnsureCursorLocked()
        {
            if (!_cursorOwned || Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _cursorOwned = true;
            }
        }

        void ReleaseCursor()
        {
            if (!_cursorOwned && Cursor.lockState == CursorLockMode.None)
                return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _cursorOwned = false;
        }

        void ApplyLook()
        {
            Vector2 delta = ReadMouseDelta();
            float sens = Mathf.Clamp(lookSensitivity, MinLookSensitivity, MaxLookSensitivity);
            _yaw += delta.x * sens * 0.12f;
            _pitch -= delta.y * sens * 0.12f;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            _origin.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_mainCamera != null)
            {
                // Pitch on Main Camera (or Camera Offset if camera is locked by XR tracking).
                _mainCamera.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
            else if (_cameraOffset != null)
            {
                _cameraOffset.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        void ApplyMove()
        {
            if (_origin == null || LabPlayerSpawnCompat.IsMonumentTransform(_origin))
                return;
            if (_cc == null || _cc.transform != _origin)
                _cc = _origin.GetComponent<CharacterController>();
            if (_cc == null)
                return;
            if (!_cc.enabled)
                _cc.enabled = true;

            Vector2 stick = ReadMoveAxes();
            bool sprint = ReadSprint();
            bool jump = ReadJump();
            bool crouch = ReadCrouch();

            // Smooth crouch
            if (crouch != _crouching)
            {
                _crouching = crouch;
            }
            float targetH = _crouching ? crouchHeight : _baseCcHeight;
            _cc.height = Mathf.Lerp(_cc.height, targetH, Time.deltaTime * 12f);
            _cc.center = new Vector3(_baseCcCenter.x, _cc.height * 0.5f, _baseCcCenter.z);

            float targetSpeed = sprint ? sprintSpeed : walkSpeed;
            Vector3 forward = _origin.forward;
            Vector3 right = _origin.right;
            forward.y = 0f;
            right.y = 0f;
            if (forward.sqrMagnitude > 1e-6f) forward.Normalize();
            if (right.sqrMagnitude > 1e-6f) right.Normalize();

            Vector3 wishDir = (forward * stick.y + right * stick.x);
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            // Acceleration / deceleration for nicer feel
            float accel = (wishDir.sqrMagnitude > 0.01f) ? acceleration : deceleration;
            _currentVelocity = Vector3.Lerp(_currentVelocity, wishDir * targetSpeed, Time.deltaTime * accel);

            // Grounding & jump
            bool grounded = _cc.isGrounded;
            if (grounded)
            {
                if (_vertVel < 0f) _vertVel = -2f;
                if (jump) _vertVel = jumpSpeed;
            }
            else
            {
                _vertVel += gravity * Time.deltaTime;
            }

            Vector3 motion = _currentVelocity;
            motion.y = _vertVel;

            CollisionFlags flags = _cc.Move(motion * Time.deltaTime);
            if ((flags & CollisionFlags.Below) != 0 && _vertVel < 0f)
                _vertVel = -2f;

        }

        static Vector2 ReadMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return Mouse.current.delta.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X") * 20f, Input.GetAxisRaw("Mouse Y") * 20f);
#else
            return Vector2.zero;
#endif
        }

        static Vector2 ReadMoveAxes()
        {
            float x = 0f, y = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (x == 0f && y == 0f)
            {
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            }
#endif
            return new Vector2(x, y);
        }

        static bool ReadSprint()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                return true;
#endif
            return false;
        }

        static bool ReadJump()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space))
                return true;
#endif
            return false;
        }

        static bool ReadCrouch()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed))
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                return true;
#endif
            return false;
        }

        static bool ReadRecalibrateHeight()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.H))
                return true;
#endif
            return false;
        }
    }

    /// <summary>String constants shared with LabPlayerSpawn without a hard XR assembly cycle.</summary>
    public static class LabPlayerSpawnCompat
    {
        public const string OriginName = "XR Origin";
        public const string CameraOffsetName = "Camera Offset";
        public const string MainCameraName = "Main Camera";

        // Match DesktopBody capsule (standing ~1.72m, shoulder half-width ~0.22).
        public const float CcHeight = 1.72f;
        public const float CcRadius = 0.22f;
        public const float CcSkin = 0.08f;
        public const float CcStepOffset = 0.30f;

        /// <summary>
        /// Shared CharacterController profile so locomotion capsule matches DesktopBody
        /// and does not fall through plaza or wedge into the lab table.
        /// </summary>
        public static void ApplyCharacterControllerProfile(CharacterController cc)
        {
            if (cc == null)
                return;
            bool was = cc.enabled;
            cc.enabled = false;
            cc.height = CcHeight;
            cc.radius = CcRadius;
            cc.skinWidth = CcSkin;
            cc.center = new Vector3(0f, CcHeight * 0.5f, 0f);
            cc.slopeLimit = 45f;
            // stepOffset must stay below height; keep modest so table lips do not snag.
            cc.stepOffset = Mathf.Min(CcStepOffset, CcHeight * 0.35f);
            cc.minMoveDistance = 0f;
            cc.enabled = true;
        }

        static readonly string[] MonumentTokens =
        {
            "pyramid", "khufu", "khafre", "menkaure", "giza", "mastaba",
            "mountainscene", "lablandscape", "sphinx", "g1a", "g1b", "g1c", "g1d", "g2", "g3"
        };

        public static bool IsMonumentName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            string n = name.ToLowerInvariant();
            // Exact player / camera names are never monuments.
            if (n == "xr origin" || n == "camera offset" || n == "main camera"
                || n == "qhysicsdesktopplayer" || n.StartsWith("[buildingblock]"))
                return false;
            for (int i = 0; i < MonumentTokens.Length; i++)
            {
                if (n.IndexOf(MonumentTokens[i], System.StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        public static bool IsMonumentTransform(Transform t)
        {
            if (t == null)
                return false;
            if (IsMonumentName(t.name))
                return true;
            // Ancestor check (player accidentally parented under Giza).
            Transform p = t.parent;
            int guard = 0;
            while (p != null && guard++ < 64)
            {
                if (IsMonumentName(p.name))
                    return true;
                p = p.parent;
            }
            return false;
        }

        public static void EnsurePlayerNotUnderMonument(Transform playerRoot)
        {
            if (playerRoot == null)
                return;
            if (playerRoot.parent == null)
                return;
            if (!IsMonumentTransform(playerRoot.parent) && !IsMonumentName(playerRoot.parent.name))
            {
                // Still unparent if any ancestor is a monument.
                Transform p = playerRoot.parent;
                bool under = false;
                int guard = 0;
                while (p != null && guard++ < 64)
                {
                    if (IsMonumentName(p.name))
                    {
                        under = true;
                        break;
                    }
                    p = p.parent;
                }
                if (!under)
                    return;
            }
            Debug.LogWarning("DesktopPlayer: unparenting '" + playerRoot.name + "' from monument hierarchy '" +
                             (playerRoot.parent != null ? playerRoot.parent.name : "?") + "' to scene root.");
            playerRoot.SetParent(null, true);
        }

        public static void StripMonumentChildren(Transform playerRoot)
        {
            if (playerRoot == null)
                return;
            for (int i = playerRoot.childCount - 1; i >= 0; i--)
            {
                Transform c = playerRoot.GetChild(i);
                if (c == null)
                    continue;
                if (c.name == CameraOffsetName || c.name == "QhysicsDesktopPlayer" || c.name == "DesktopBody")
                    continue;
                if (!IsMonumentName(c.name))
                    continue;
                Debug.LogWarning("DesktopPlayer: stripping monument child '" + c.name + "' from player root to scene root.");
                c.SetParent(null, true);
            }
        }

        public static void EnsureDesktopViewAuthority(Transform origin, Transform xrMainCamera)
        {
            if (origin == null)
                return;

            // Disable leftover OVR / BuildingBlock cameras so WASD moves the view the user sees.
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Camera keep = null;
            if (xrMainCamera != null)
                keep = xrMainCamera.GetComponent<Camera>();
            if (keep == null)
            {
                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i] != null && cams[i].transform.IsChildOf(origin))
                    {
                        keep = cams[i];
                        break;
                    }
                }
            }

            for (int i = 0; i < cams.Length; i++)
            {
                Camera c = cams[i];
                if (c == null)
                    continue;
                bool underOrigin = c.transform.IsChildOf(origin);
                string n = c.gameObject.name ?? "";
                bool rival = !underOrigin && (
                    n.IndexOf("CenterEye", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("LeftEye", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("RightEye", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("BuildingBlock", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || IsUnderBuildingBlock(c.transform));
                if (rival)
                {
                    if (c.enabled)
                        c.enabled = false;
                    if (c.CompareTag("MainCamera"))
                        c.tag = "Untagged";
                    var al = c.GetComponent<AudioListener>();
                    if (al != null && al.enabled)
                        al.enabled = false;
                }
            }

            if (keep != null)
            {
                if (!keep.enabled)
                    keep.enabled = true;
                if (!keep.CompareTag("MainCamera"))
                    keep.tag = "MainCamera";
                var listener = keep.GetComponent<AudioListener>();
                if (listener != null && !listener.enabled)
                    listener.enabled = true;

                // TrackedPoseDriver fights mouse-look / makes desktop feel "stuck".
                var behaviours = keep.GetComponents<Behaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    Behaviour b = behaviours[i];
                    if (b == null || !b.enabled)
                        continue;
                    string tn = b.GetType().Name;
                    if (tn.IndexOf("TrackedPoseDriver", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        b.enabled = false;
                }
            }

            // Soft-disable empty BuildingBlock camera rig so it cannot steal focus.
            GameObject bb = GameObject.Find("[BuildingBlock] Camera Rig");
            if (bb != null && bb.activeSelf)
                bb.SetActive(false);
        }

        static bool IsUnderBuildingBlock(Transform t)
        {
            Transform p = t;
            int guard = 0;
            while (p != null && guard++ < 64)
            {
                if (p.name != null && p.name.IndexOf("BuildingBlock", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                p = p.parent;
            }
            return false;
        }
    }
}
