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
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            for (int i = 0; i < displays.Count; i++)
            {
                if (displays[i] != null && displays[i].running)
                    return true;
            }
#pragma warning disable CS0618
            try
            {
                if (XRSettings.isDeviceActive && !string.IsNullOrEmpty(XRSettings.loadedDeviceName)
                    && XRSettings.loadedDeviceName != "None")
                    return true;
            }
            catch { /* XRSettings may be unavailable */ }
#pragma warning restore CS0618
            return false;
        }

        public void Bind(Transform origin)
        {
            _origin = origin;
            if (_origin == null)
                return;
            _cc = _origin.GetComponent<CharacterController>();
            if (_cc != null)
            {
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
            if (_mainCamera == null && Camera.main != null)
                _mainCamera = Camera.main.transform;

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
            if (_cc == null)
                _cc = _origin.GetComponent<CharacterController>();
            if (_cc == null || !_cc.enabled)
                return;

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
    static class LabPlayerSpawnCompat
    {
        public const string OriginName = "XR Origin";
        public const string CameraOffsetName = "Camera Offset";
        public const string MainCameraName = "Main Camera";
    }
}
