using System;
using RealityEngine.Player;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace RealityEngine.UI
{
    /// <summary>
    /// Primary summoned toolbelt: BUILD / PHYSICS / MEASURE / WORLD / EXPERIMENTS tabs + tool chips.
    /// Toggle via menu key, B button, or grip (Input System / legacy fallback).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(201)]
    public sealed class QhysicsToolbelt : MonoBehaviour
    {
        public const string RootName = "QhysicsToolbelt";

        public enum Category { Build = 0, Physics = 1, Measure = 2, World = 3, Experiments = 4 }

        static readonly string[] TabNames = { "BUILD", "PHYSICS", "MEASURE", "WORLD", "EXPERIMENTS" };
        static readonly string[][] ChipSets =
        {
            new[] { "Wire", "Battery", "Switch", "Bulb", "Resistor", "Lamp", "Motor", "Solar", "Capacitor", "Inductor", "Diode", "Fuse", "LED", "Speaker", "Potentiometer", "Transformer", "Function Generator" },
            new[] { "Magnet", "Coil", "Field Lens", "Dipole" },
            new[] { "Multimeter", "Galvanometer", "Oscilloscope", "Frequency Counter", "Power Meter", "Flux Meter", "Charge Meter", "Voltmeter", "Ammeter", "Ohmmeter", "Capacitance Meter", "Inductance Meter", "Resonance Meter", "Impedance Meter", "Power Factor Meter", "Cubit Rod", "Probe", "Stopwatch" },
            new[] { "Teleport", "Scale", "Sky", "Reset Pose" },
            new[] { "Induction", "New Run", "Save", "Load" }
        };

        [SerializeField] bool visible;
        [SerializeField] float hipFollowLag = 14f;  // snappier follow for polish
        [SerializeField] float beltForwardM = 0.35f;
        [SerializeField] float beltUpM = 0.12f;
        Category _category = Category.Build;
        Canvas _canvas;
        RectTransform _chipRow;
        Image[] _tabImages;
        Camera _cam;
        Transform _hip;
        float _spawnCooldown;

        public bool IsVisible => visible;

        public static QhysicsToolbelt Ensure(Transform parent)
        {
            Transform existing = parent != null ? parent.Find(RootName) : null;
            if (existing == null)
            {
                GameObject found = GameObject.Find(RootName);
                if (found != null)
                    existing = found.transform;
            }
            if (existing != null)
            {
                var t = existing.GetComponent<QhysicsToolbelt>();
                if (t == null)
                    t = existing.gameObject.AddComponent<QhysicsToolbelt>();
                if (t._canvas == null)
                    t.Build();
                return t;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsToolbelt>();
            comp.Build();
            return comp;
        }

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(1100f, 420f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(1060f, 380f));

            var title = QhysicsUiBuilder.Label(face.transform, "Title", "TOOLBELT", QhysicsUiStyle.FontBody,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.anchoredPosition = new Vector2(-420f, 150f);
            title.rectTransform.sizeDelta = new Vector2(240f, 36f);

            var hint = QhysicsUiBuilder.Label(face.transform, "Hint", "Menu / B / Grip to toggle", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextMuted, TextAlignmentOptions.MidlineRight);
            hint.rectTransform.anchoredPosition = new Vector2(280f, 150f);
            hint.rectTransform.sizeDelta = new Vector2(360f, 28f);

            var tabRowGo = new GameObject("Tabs", typeof(RectTransform));
            tabRowGo.transform.SetParent(face.transform, false);
            var tabRow = tabRowGo.GetComponent<RectTransform>();
            tabRow.anchoredPosition = new Vector2(0f, 90f);
            tabRow.sizeDelta = new Vector2(1000f, QhysicsUiStyle.TargetMinPx);
            QhysicsUiBuilder.LayoutHorizontal(tabRow, 10f);
            _tabImages = new Image[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i;
                var btn = QhysicsUiBuilder.ChipButton(tabRow, "Tab_" + TabNames[i], TabNames[i],
                    new Vector2(180f, QhysicsUiStyle.TargetMinPx), () => SelectCategory((Category)idx));
                _tabImages[i] = btn.targetGraphic as Image;
            }

            var chipGo = new GameObject("Chips", typeof(RectTransform));
            chipGo.transform.SetParent(face.transform, false);
            _chipRow = chipGo.GetComponent<RectTransform>();
            _chipRow.anchoredPosition = new Vector2(0f, -40f);
            _chipRow.sizeDelta = new Vector2(1000f, 160f);
            QhysicsUiBuilder.LayoutHorizontal(_chipRow, 14f);

            SelectCategory(Category.Build);
            SetVisible(false);
        }

        void Update()
        {
            if (WasTogglePressed())
                Toggle();
        }

        void LateUpdate()
        {
            if (!visible)
                return;
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_hip == null)
                _hip = ResolveHipAnchor();
            if (_cam == null)
                return;

            // Worn at hip/chest of DesktopBody / XR Origin Ã¢â‚¬â€ lag follow, not glued to HMD.
            Vector3 anchorPos;
            Vector3 flatFwd;
            if (_hip != null)
            {
                flatFwd = Flatten(_hip.forward);
                if (flatFwd.sqrMagnitude < 1e-6f)
                    flatFwd = Flatten(_cam.transform.forward);
                anchorPos = _hip.position + flatFwd * beltForwardM + Vector3.up * beltUpM;
            }
            else
            {
                flatFwd = Flatten(_cam.transform.forward);
                // Fallback: chest height in front of body yaw, not raw HMD pitch.
                float y = _cam.transform.position.y - 0.55f;
                anchorPos = new Vector3(_cam.transform.position.x, y, _cam.transform.position.z)
                    + flatFwd * beltForwardM;
            }

            float k = 1f - Mathf.Exp(-hipFollowLag * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, anchorPos, k);
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
        }

        static Transform ResolveHipAnchor()
        {
            GameObject originGo = GameObject.Find(LabPlayerSpawnCompat.OriginName);
            Transform origin = originGo != null ? originGo.transform : null;
            return QhysicsDesktopBootstrap.FindHipAnchor(origin);
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        public void Toggle() => SetVisible(!visible);

        public void SetVisible(bool on)
        {
            visible = on;
            if (_canvas != null)
                _canvas.gameObject.SetActive(on);
            if (on)
            {
                if (_hip == null)
                    _hip = ResolveHipAnchor();
                if (_cam == null)
                    _cam = QhysicsUiBuilder.ResolveXrCamera();
                if (_hip != null)
                {
                    Vector3 flatFwd = Flatten(_hip.forward);
                    transform.position = _hip.position + flatFwd * beltForwardM + Vector3.up * beltUpM;
                }
            }
        }

        public void SelectCategory(Category cat)
        {
            _category = cat;
            for (int i = 0; i < _tabImages.Length; i++)
            {
                if (_tabImages[i] != null)
                    _tabImages[i].color = i == (int)cat ? QhysicsUiStyle.TabActive : QhysicsUiStyle.TabIdle;
            }
            RebuildChips();
        }

        void RebuildChips()
        {
            if (_chipRow == null)
                return;
            for (int i = _chipRow.childCount - 1; i >= 0; i--)
            {
                Transform c = _chipRow.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }
            string[] chips = ChipSets[(int)_category];
            for (int i = 0; i < chips.Length; i++)
            {
                string label = chips[i];
                QhysicsUiBuilder.ChipButton(_chipRow, "Chip_" + label, label,
                    new Vector2(160f, 96f), () => OnChip(label));
            }
        }

        void OnChip(string label)
        {
            if (Time.unscaledTime < _spawnCooldown)
                return;
            _spawnCooldown = Time.unscaledTime + 0.2f;
            // chip selected Ã¢â‚¬â€ use table dispensers to spawn
            TryCircuitLabHint(label);
        }

        static void TryCircuitLabHint(string label)
        {
            // Spawn via shared gadget API (desktop hotbar + VR toolbelt Build chips)
            Camera cam = QhysicsUiBuilder.ResolveXrCamera();
            Vector3 pos = cam != null
                ? cam.transform.position + cam.transform.forward * 1.1f + Vector3.up * 0.1f
                : Vector3.zero;
            if (QhysicsGadgets.SpawnByLabel(label, pos) != null)
                return;
            var lab = UnityEngine.Object.FindAnyObjectByType<CircuitLab>(FindObjectsInactive.Include);
            if (lab == null)
                return;
        }

        bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.mKey.wasPressedThisFrame
                || Keyboard.current.tabKey.wasPressedThisFrame))
                return true;
            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
                return true;
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (!(devices[i] is UnityEngine.InputSystem.XR.XRController xr))
                    continue;
                if (TryButton(xr, "menuButton") || TryButton(xr, "secondaryButton")
                    || TryButton(xr, "gripButton"))
                    return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton6)
                || Input.GetKeyDown(KeyCode.JoystickButton2))
                return true;
#endif
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        static bool TryButton(InputDevice device, string path)
        {
            try
            {
                var ctrl = device.TryGetChildControl<ButtonControl>(path);
                return ctrl != null && ctrl.wasPressedThisFrame;
            }
            catch
            {
                return false;
            }
        }
#endif
    }
}

