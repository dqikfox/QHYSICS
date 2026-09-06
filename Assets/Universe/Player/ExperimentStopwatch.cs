using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable MEASURE stopwatch: wall-clock timer for timing magnet sweeps / runs.
    /// Honesty: Editor/player Time.unscaledTime — not a sim clock or atomic standard.
    /// Desktop: T toggle, Y reset (when near/held). VR: activate (trigger) toggles while selected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExperimentStopwatch : MonoBehaviour
    {
        public const string RootName = "Gadget_Stopwatch";
        public const string Honesty = "Wall-clock unscaled time for experiment timing. Not a sim clock.";

        TextMeshPro _readout;
        XRGrabInteractable _grab;
        bool _running;
        float _elapsed;
        float _lap;
        float _startedAt;
        float _inputCooldown;

        public void EnsureBuilt()
        {
            if (_readout == null)
                BuildReadout();
            WireGrab();
            RefreshText();
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void OnEnable()
        {
            WireGrab();
        }

        void OnDisable()
        {
            UnwireGrab();
        }

        void Update()
        {
            if (_running)
                _elapsed = _lap + (Time.unscaledTime - _startedAt);

            if (IsActiveContext() && Time.unscaledTime >= _inputCooldown)
            {
                if (WasTogglePressed())
                {
                    _inputCooldown = Time.unscaledTime + 0.2f;
                    Toggle();
                }
                else if (WasResetPressed())
                {
                    _inputCooldown = Time.unscaledTime + 0.2f;
                    ResetTimer();
                }
            }

            RefreshText();
        }

        void WireGrab()
        {
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
            _grab.activated.AddListener(OnActivated);
        }

        void UnwireGrab()
        {
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
        }

        void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
        {
            if (Time.unscaledTime < _inputCooldown)
                return;
            _inputCooldown = Time.unscaledTime + 0.2f;
            Toggle();
        }

        bool IsActiveContext()
        {
            if (_grab != null && _grab.isSelected)
                return true;
            Camera cam = Camera.main;
            if (cam == null)
                return false;
            return (transform.position - cam.transform.position).sqrMagnitude < 2.5f * 2.5f;
        }

        static bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.T))
                return true;
#endif
            return false;
        }

        static bool WasResetPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Y))
                return true;
#endif
            return false;
        }

        public void Toggle()
        {
            if (_running)
            {
                _elapsed = _lap + (Time.unscaledTime - _startedAt);
                _lap = _elapsed;
                _running = false;
            }
            else
            {
                _startedAt = Time.unscaledTime;
                _running = true;
            }
            RefreshText();
        }

        public void ResetTimer()
        {
            _running = false;
            _elapsed = 0f;
            _lap = 0f;
            _startedAt = 0f;
            RefreshText();
        }

        void BuildReadout()
        {
            Transform existing = transform.Find("Readout");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("Readout");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.045f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 28f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.95f, 0.85f, 0.35f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(28f, 14f);
            _readout.text = "STOPWATCH\n00:00.00\nT toggle / Y reset";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;
            string state = _running ? "RUN" : (_elapsed > 0f ? "STOP" : "READY");
            _readout.text =
                "STOPWATCH " + state + "\n"
                + Format(_elapsed) + "\n"
                + "T toggle / Y reset\n"
                + "[wall-clock]";
        }

        static string Format(float seconds)
        {
            if (seconds < 0f)
                seconds = 0f;
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return m.ToString("00") + ":" + s.ToString("00.00");
        }
    }
}
