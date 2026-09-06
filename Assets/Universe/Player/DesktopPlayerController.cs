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

        [SerializeField] float walkSpeed = 3.2f;
        [SerializeField] float sprintSpeed = 6.5f;
        [SerializeField] float lookSensitivity = 1.6f;
        [SerializeField] float jumpSpeed = 5.5f;
        [SerializeField] float crouchHeight = 1.0f;
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float gravity = -18f;
        [SerializeField] float minPitch = -80f;
        [SerializeField] float maxPitch = 80f;

        Transform _origin;
        Transform _cameraOffset;
        Transform _mainCamera;
        CharacterController _cc;
        float _pitch;
        float _yaw;
        float _vertVel;
        bool _crouching;
        bool _desktopActive;
        bool _cursorOwned;
        float _baseCcHeight = 1.8f;
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
                if (!_cc.enabled)
                    _cc.enabled = true;
                _baseCcHeight = _cc.height > 0.1f ? _cc.height : standHeight;
                _baseCcCenter = _cc.center;
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
            _yaw += delta.x * lookSensitivity * 0.12f;
            _pitch -= delta.y * lookSensitivity * 0.12f;
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

            if (crouch != _crouching)
            {
                _crouching = crouch;
                float h = _crouching ? crouchHeight : _baseCcHeight;
                bool was = _cc.enabled;
                _cc.enabled = false;
                _cc.height = h;
                _cc.center = new Vector3(_baseCcCenter.x, h * 0.5f, _baseCcCenter.z);
                _cc.enabled = was;
            }

            float speed = sprint ? sprintSpeed : walkSpeed;
            Vector3 forward = _origin.forward;
            Vector3 right = _origin.right;
            forward.y = 0f;
            right.y = 0f;
            if (forward.sqrMagnitude > 1e-6f) forward.Normalize();
            if (right.sqrMagnitude > 1e-6f) right.Normalize();
            Vector3 wish = (forward * stick.y + right * stick.x);
            if (wish.sqrMagnitude > 1f)
                wish.Normalize();

            if (_cc.isGrounded)
            {
                _vertVel = -1f;
                if (jump)
                    _vertVel = jumpSpeed;
            }
            else
                _vertVel += gravity * Time.deltaTime;

            Vector3 motion = wish * speed;
            motion.y = _vertVel;
            _cc.Move(motion * Time.deltaTime);
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
    }

    /// <summary>String constants shared with LabPlayerSpawn without a hard XR assembly cycle.</summary>
    public static class LabPlayerSpawnCompat
    {
        public const string OriginName = "XR Origin";
        public const string CameraOffsetName = "Camera Offset";
        public const string MainCameraName = "Main Camera";

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
                if (c.name == CameraOffsetName || c.name == "QhysicsDesktopPlayer")
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
