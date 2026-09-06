using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RealityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Desktop hotbar slots 1â€“8 + screen overlay. VR toolbelt remains separate.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(132)]
    public sealed class QhysicsInventory : MonoBehaviour
    {
        public const string RootName = "QhysicsInventory";

        public enum SlotId
        {
            Wire = 0,
            Battery = 1,
            Switch = 2,
            Bulb = 3,
            Magnet = 4,
            FieldLens = 5,
            CubitRod = 6,
            Empty = 7
        }

        public static readonly string[] SlotLabels =
        {
            "Wire", "Battery", "Switch", "Bulb", "Magnet", "Field Lens", "Cubit Rod", "Delete"
        };

        public static QhysicsInventory Instance { get; private set; }

        int _selected;
        Canvas _canvas;
        Image[] _slotImages;
        TextMeshProUGUI[] _slotLabels;
        TextMeshProUGUI _hint;
        GameObject _ghost;
        Transform _ghostParent;

        public int SelectedIndex => _selected;
        public SlotId SelectedSlot => (SlotId)_selected;
        public string SelectedLabel => SlotLabels[Mathf.Clamp(_selected, 0, SlotLabels.Length - 1)];

        public static QhysicsInventory Ensure(Transform parent)
        {
            QhysicsInventory existing = Object.FindFirstObjectByType<QhysicsInventory>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing._canvas == null)
                    existing.BuildUi();
                return existing;
            }
            var go = new GameObject(RootName);
            if (parent != null)
                go.transform.SetParent(parent, false);
            var inv = go.AddComponent<QhysicsInventory>();
            inv.BuildUi();
            return inv;
        }

        void OnEnable() => Instance = this;
        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            var desktop = DesktopPlayerController.Instance;
            bool show = desktop != null && desktop.IsDesktopActive;
            if (_canvas != null && _canvas.gameObject.activeSelf != show)
                _canvas.gameObject.SetActive(show);
            if (!show)
            {
                ClearGhost();
                return;
            }

            HandleHotkeys();
            UpdateGhost();
            RefreshHighlights();
        }

        void HandleHotkeys()
        {
            int digit = ReadDigitPressed();
            if (digit >= 1 && digit <= 8)
                Select(digit - 1);

            int scroll = ReadScrollCycle();
            if (scroll != 0)
                Select((_selected + scroll + SlotLabels.Length) % SlotLabels.Length);

            if (WasQPressed())
                Select((_selected + SlotLabels.Length - 1) % SlotLabels.Length);
        }

        public void Select(int index)
        {
            _selected = Mathf.Clamp(index, 0, SlotLabels.Length - 1);
            RefreshHighlights();
            RebuildGhost();
        }

        void BuildUi()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            var canvasGo = new GameObject("HotbarCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var bar = QhysicsUiBuilder.Panel(canvasGo.transform, "Bar", QhysicsUiStyle.PanelBg, new Vector2(920f, 96f));
            var barRt = bar.rectTransform;
            barRt.anchorMin = new Vector2(0.5f, 0f);
            barRt.anchorMax = new Vector2(0.5f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = new Vector2(0f, 28f);

            _slotImages = new Image[8];
            _slotLabels = new TextMeshProUGUI[8];
            float slotW = 100f;
            float startX = -((8 - 1) * (slotW + 8f)) * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                var slot = QhysicsUiBuilder.Panel(bar.transform, "Slot_" + (i + 1), QhysicsUiStyle.ChipBg, new Vector2(slotW, 78f));
                slot.rectTransform.anchoredPosition = new Vector2(startX + i * (slotW + 8f), 0f);
                _slotImages[i] = slot;

                var key = QhysicsUiBuilder.Label(slot.transform, "Key", (i + 1).ToString(), QhysicsUiStyle.FontSmall,
                    QhysicsUiStyle.TextMuted, TextAlignmentOptions.Top);
                key.rectTransform.anchoredPosition = new Vector2(0f, 28f);
                key.rectTransform.sizeDelta = new Vector2(90f, 24f);

                var lab = QhysicsUiBuilder.Label(slot.transform, "Label", SlotLabels[i], QhysicsUiStyle.FontChip,
                    QhysicsUiStyle.TextPrimary, TextAlignmentOptions.Center);
                lab.rectTransform.anchoredPosition = new Vector2(0f, -6f);
                lab.rectTransform.sizeDelta = new Vector2(94f, 40f);
                lab.enableAutoSizing = true;
                lab.fontSizeMin = 14f;
                lab.fontSizeMax = QhysicsUiStyle.FontChip;
                _slotLabels[i] = lab;
            }

            _hint = QhysicsUiBuilder.Label(canvasGo.transform, "Hint",
                "WASD move Â· Mouse look Â· 1-8 tools Â· E grab Â· F/R drop Â· Esc pause",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            _hint.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            _hint.rectTransform.anchoredPosition = new Vector2(0f, 132f);
            _hint.rectTransform.sizeDelta = new Vector2(900f, 28f);

            RefreshHighlights();
        }

        void RefreshHighlights()
        {
            if (_slotImages == null)
                return;
            for (int i = 0; i < _slotImages.Length; i++)
            {
                if (_slotImages[i] == null)
                    continue;
                _slotImages[i].color = i == _selected ? QhysicsUiStyle.ChipBgActive : QhysicsUiStyle.ChipBg;
                if (_slotLabels[i] != null)
                    _slotLabels[i].color = i == _selected ? QhysicsUiStyle.AccentInfo : QhysicsUiStyle.TextPrimary;
            }
        }

        void RebuildGhost()
        {
            ClearGhost();
            var slot = SelectedSlot;
            if (slot == SlotId.Empty)
                return;

            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghost.name = "ToolGhost_" + SelectedLabel;
            Object.Destroy(_ghost.GetComponent<Collider>());
            _ghost.transform.localScale = new Vector3(0.08f, 0.04f, 0.12f);
            var r = _ghost.GetComponent<Renderer>();
            if (r != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.25f, 0.85f, 0.95f, 0.35f);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(0.25f, 0.85f, 0.95f, 0.35f));
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1f);
                r.sharedMaterial = mat;
            }
            _ghostParent = null;
        }

        void UpdateGhost()
        {
            if (_ghost == null)
                return;
            var desktop = DesktopPlayerController.Instance;
            Camera cam = null;
            if (desktop != null && desktop.MainCamera != null)
                cam = desktop.MainCamera.GetComponent<Camera>();
            if (cam == null)
                cam = Camera.main;
            if (cam == null)
                return;
            _ghost.transform.position = cam.transform.position + cam.transform.forward * 0.55f + cam.transform.right * 0.18f
                + cam.transform.up * -0.12f;
            _ghost.transform.rotation = cam.transform.rotation;
        }

        void ClearGhost()
        {
            if (_ghost == null)
                return;
            if (Application.isPlaying) Destroy(_ghost);
            else DestroyImmediate(_ghost);
            _ghost = null;
        }

        static int ReadDigitPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 1;
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 2;
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 3;
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 4;
                if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) return 5;
                if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) return 6;
                if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) return 7;
                if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) return 8;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 3;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 4;
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) return 5;
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) return 6;
            if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) return 7;
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) return 8;
#endif
            return 0;
        }

        static int ReadScrollCycle()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                float y = Mouse.current.scroll.ReadValue().y;
                if (y > 0.1f) return -1;
                if (y < -0.1f) return 1;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            float s = Input.mouseScrollDelta.y;
            if (s > 0.1f) return -1;
            if (s < -0.1f) return 1;
#endif
            return 0;
        }

        static bool WasQPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Q))
                return true;
#endif
            return false;
        }
    }
}

