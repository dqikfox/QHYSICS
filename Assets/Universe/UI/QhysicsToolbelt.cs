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
            new[] { "Magnet", "Coil", "Field Lens", "Dipole", "Crank Generator" },
            new[] { "Multimeter", "Galvanometer", "Oscilloscope", "Frequency Counter", "Power Meter", "Flux Meter", "Charge Meter", "Voltmeter", "Ammeter", "Ohmmeter", "Capacitance Meter", "Inductance Meter", "Resonance Meter", "Impedance Meter", "Power Factor Meter", "Q Factor Meter", "Admittance Meter", "Decibel Meter", "Crest Factor Meter", "Energy Meter", "Duty Cycle Meter", "Slew Rate Meter", "Rise/Fall Meter", "Overshoot Meter", "Peak-to-Peak Meter", "Mean Meter", "Ripple Meter", "THD Meter", "Cubit Rod", "Probe", "Compass", "Stopwatch" },
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
        int _chipPage;
        TextMeshProUGUI _pageLabel;
        const int ChipsPerPage = 5;

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

            _pageLabel = QhysicsUiBuilder.Label(face.transform, "PageHint", "", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            _pageLabel.rectTransform.anchoredPosition = new Vector2(0f, -150f);
            _pageLabel.rectTransform.sizeDelta = new Vector2(900f, 28f);

            SelectCategory(Category.Build);
            SetVisible(false);
        }

        void Update()
        {
            if (WasTogglePressed())
                Toggle();
            if (visible)
                TryScrollChipPage();
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

            // Worn at hip/chest of DesktopBody / XR Origin â€” lag follow, not glued to HMD.
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
            _chipPage = 0;
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
            int pageCount = Mathf.Max(1, (chips.Length + ChipsPerPage - 1) / ChipsPerPage);
            if (_chipPage < 0) _chipPage = 0;
            if (_chipPage >= pageCount) _chipPage = pageCount - 1;

            bool multi = pageCount > 1;
            if (multi)
            {
                QhysicsUiBuilder.ChipButton(_chipRow, "Chip_PrevPage", "<",
                    new Vector2(72f, 96f), () => ShiftChipPage(-1));
            }

            int start = _chipPage * ChipsPerPage;
            int end = Mathf.Min(chips.Length, start + ChipsPerPage);
            for (int i = start; i < end; i++)
            {
                string label = chips[i];
                QhysicsUiBuilder.ChipButton(_chipRow, "Chip_" + label, label,
                    new Vector2(160f, 96f), () => OnChip(label));
            }

            if (multi)
            {
                QhysicsUiBuilder.ChipButton(_chipRow, "Chip_NextPage", ">",
                    new Vector2(72f, 96f), () => ShiftChipPage(1));
            }

            if (_pageLabel != null)
            {
                if (multi)
                    _pageLabel.text = TabNames[(int)_category] + "  " + (_chipPage + 1) + "/" + pageCount
                        + "  (scroll or < >)";
                else
                    _pageLabel.text = "";
            }
        }

        void ShiftChipPage(int delta)
        {
            string[] chips = ChipSets[(int)_category];
            int pageCount = Mathf.Max(1, (chips.Length + ChipsPerPage - 1) / ChipsPerPage);
            int next = _chipPage + delta;
            if (next < 0) next = pageCount - 1;
            if (next >= pageCount) next = 0;
            if (next == _chipPage)
                return;
            _chipPage = next;
            RebuildChips();
        }

        void TryScrollChipPage()
        {
            string[] chips = ChipSets[(int)_category];
            int pageCount = Mathf.Max(1, (chips.Length + ChipsPerPage - 1) / ChipsPerPage);
            if (pageCount <= 1)
                return;
            int delta = 0;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                float y = Mouse.current.scroll.ReadValue().y;
                if (y > 0.1f) delta = -1;
                else if (y < -0.1f) delta = 1;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (delta == 0)
            {
                float s = Input.mouseScrollDelta.y;
                if (s > 0.1f) delta = -1;
                else if (s < -0.1f) delta = 1;
            }
#endif
            if (delta != 0)
                ShiftChipPage(delta);
        }

        void OnChip(string label)
        {
            if (Time.unscaledTime < _spawnCooldown)
                return;
            _spawnCooldown = Time.unscaledTime + 0.2f;
            if (TryLabAction(label))
                return;
            TrySpawnGadget(label);
        }

        static bool TryLabAction(string label)
        {
            if (string.IsNullOrEmpty(label))
                return false;
            string key = label.Trim().ToLowerInvariant();

            if (key == "new run" || key == "reset" || key == "reset experiment")
            {
                QhysicsLabActions.ResetCircuitLab();
                return true;
            }
            if (key == "sky")
            {
                QhysicsLabActions.CycleSky();
                return true;
            }
            if (key == "teleport" || key == "reset pose")
            {
                RealityEngine.XR.LabPlayerSpawn.EnsureApplied();
                RealityEngine.XR.LabPlayerSpawn.RecalibratePlayerHeight(force: true);
                Debug.Log("QHYSICS: " + label + " â€” plaza pose + eye height recalibrated.");
                return true;
            }
            if (key == "scale")
            {
                Debug.Log("QHYSICS: Scale chip reserved (world scale locked). Use SimChip for timeScale.");
                return true;
            }
            if (key == "save" || key == "load")
            {
                Debug.Log("QHYSICS: " + label + " stub â€” persistence comes with experiment runner save slots.");
                return true;
            }
            if (key == "induction")
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var lab = RealityEngine.Experiments.InductionLabBootstrap.EnsureLabInScene(scene);
                if (lab != null)
                    lab.BuildLab();
                Debug.Log("QHYSICS: Induction lab ensured.");
                return true;
            }
            return false;
        }

        static void TrySpawnGadget(string label)
        {
            Camera cam = QhysicsUiBuilder.ResolveXrCamera();
            Vector3 pos = cam != null
                ? cam.transform.position + cam.transform.forward * 1.1f + Vector3.up * 0.1f
                : Vector3.zero;
            if (QhysicsGadgets.SpawnByLabel(label, pos) != null)
                return;
            UnityEngine.Object.FindAnyObjectByType<CircuitLab>(FindObjectsInactive.Include);
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

