using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RealityEngine.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.UI
{
    /// <summary>
    /// Screen HUD: Training System vitals + carry slots. Extends hip-pouch story without replacing BUILD hotbar.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(201)]
    public sealed class QhysicsCarryHud : MonoBehaviour
    {
        public const string RootName = "QhysicsCarryHud";

        Canvas _canvas;
        Image _hpFill;
        TextMeshProUGUI _hpLabel;
        TextMeshProUGUI _opLabel;
        TextMeshProUGUI _hint;
        Image[] _slotImages;
        TextMeshProUGUI[] _slotLabels;
        PlayerCarryInventory _carry;
        PlayerVitality _vitals;
        PlayerOperatorController _op;

        public static QhysicsCarryHud Ensure(Transform parent)
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
                var c = existing.GetComponent<QhysicsCarryHud>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsCarryHud>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsCarryHud>();
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

            var canvasGo = new GameObject("CarryHudCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 190;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            // Training vitals — top left
            var vitals = QhysicsUiBuilder.Panel(canvasGo.transform, "Vitals", QhysicsUiStyle.PanelBg, new Vector2(360f, 96f));
            var vrt = vitals.rectTransform;
            vrt.anchorMin = new Vector2(0f, 1f);
            vrt.anchorMax = new Vector2(0f, 1f);
            vrt.pivot = new Vector2(0f, 1f);
            vrt.anchoredPosition = new Vector2(24f, -24f);

            _opLabel = QhysicsUiBuilder.Label(vitals.transform, "Op", "TRAINING SYSTEM", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            _opLabel.rectTransform.anchoredPosition = new Vector2(8f, 28f);
            _opLabel.rectTransform.sizeDelta = new Vector2(330f, 28f);

            var barBg = QhysicsUiBuilder.Panel(vitals.transform, "HpBg", QhysicsUiStyle.ChipBg, new Vector2(300f, 18f));
            barBg.rectTransform.anchoredPosition = new Vector2(8f, 0f);
            barBg.rectTransform.anchorMin = barBg.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            barBg.rectTransform.pivot = new Vector2(0f, 0.5f);

            _hpFill = QhysicsUiBuilder.Panel(barBg.transform, "HpFill", QhysicsUiStyle.AccentActive, new Vector2(300f, 18f));
            _hpFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            _hpFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _hpFill.rectTransform.anchoredPosition = Vector2.zero;
            _hpFill.rectTransform.sizeDelta = new Vector2(300f, 0f);

            _hpLabel = QhysicsUiBuilder.Label(vitals.transform, "HpText", "HP 100/100", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.MidlineLeft);
            _hpLabel.rectTransform.anchoredPosition = new Vector2(8f, -28f);
            _hpLabel.rectTransform.sizeDelta = new Vector2(330f, 24f);

            // Carry strip — above BUILD hotbar
            var strip = QhysicsUiBuilder.Panel(canvasGo.transform, "CarryStrip", QhysicsUiStyle.PanelBg, new Vector2(720f, 78f));
            var srt = strip.rectTransform;
            srt.anchorMin = new Vector2(0.5f, 0f);
            srt.anchorMax = new Vector2(0.5f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 140f);

            _slotImages = new Image[8];
            _slotLabels = new TextMeshProUGUI[8];
            float slotW = 78f;
            float startX = -((8 - 1) * (slotW + 6f)) * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                var slot = QhysicsUiBuilder.Panel(strip.transform, "C" + i, QhysicsUiStyle.ChipBg, new Vector2(slotW, 62f));
                slot.rectTransform.anchoredPosition = new Vector2(startX + i * (slotW + 6f), 0f);
                _slotImages[i] = slot;
                var lab = QhysicsUiBuilder.Label(slot.transform, "L", "—", QhysicsUiStyle.FontSmall,
                    QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
                lab.rectTransform.sizeDelta = new Vector2(74f, 50f);
                lab.enableAutoSizing = true;
                lab.fontSizeMin = 10f;
                lab.fontSizeMax = 16f;
                _slotLabels[i] = lab;
            }

            _hint = QhysicsUiBuilder.Label(canvasGo.transform, "CarryHint",
                "Carry [ ]  U use/equip  X drop  O operator  LMB baton (Training)",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            _hint.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            _hint.rectTransform.anchoredPosition = new Vector2(0f, 222f);
            _hint.rectTransform.sizeDelta = new Vector2(900f, 24f);
        }

        void OnEnable()
        {
            Hook();
        }

        void OnDisable()
        {
            Unhook();
        }

        void Hook()
        {
            _carry = PlayerCarryInventory.Instance ?? PlayerCarryInventory.Ensure();
            _vitals = PlayerVitality.Instance ?? PlayerVitality.Ensure();
            _op = PlayerOperatorController.Instance;
            if (_carry != null)
                _carry.Changed += RefreshCarry;
            if (_vitals != null)
                _vitals.Changed += RefreshVitals;
            RefreshCarry();
            RefreshVitals();
        }

        void Unhook()
        {
            if (_carry != null)
                _carry.Changed -= RefreshCarry;
            if (_vitals != null)
                _vitals.Changed -= RefreshVitals;
        }

        void Update()
        {
            if (_carry == null)
                Hook();
            HandleCarryKeys();
            // Desktop-only strip; hide when XR display running (toolbelt owns VR)
            bool show = DesktopPlayerController.Instance == null || DesktopPlayerController.Instance.IsDesktopActive;
            if (_canvas != null && _canvas.gameObject.activeSelf != show)
                _canvas.gameObject.SetActive(show);
        }

        void HandleCarryKeys()
        {
            if (_carry == null)
                return;
            var pause = Object.FindFirstObjectByType<QhysicsPausePanel>(FindObjectsInactive.Include);
            if (pause != null && pause.IsOpen)
                return;
            var opPanel = Object.FindFirstObjectByType<QhysicsOperatorSelectPanel>(FindObjectsInactive.Include);
            if (opPanel != null && opPanel.IsOpen)
                return;

            if (WasPrev())
                _carry.SelectDelta(-1);
            if (WasNext())
                _carry.SelectDelta(1);
            if (WasUse())
                _carry.TryUseOrEquipSelected();
            if (WasDrop())
            {
                if (_carry.TryDropSelected(out CarryItemId id, out Vector3 pos))
                    QhysicsWorldPickup.Spawn(id, pos);
            }
        }

        void RefreshVitals()
        {
            if (_vitals == null)
                _vitals = PlayerVitality.Instance;
            if (_hpFill != null && _vitals != null)
            {
                float w = 300f * _vitals.Hp01;
                _hpFill.rectTransform.sizeDelta = new Vector2(w, 0f);
                _hpFill.color = _vitals.HasShield ? new Color(0.4f, 0.7f, 1f) :
                    (_vitals.Hp01 < 0.3f ? QhysicsUiStyle.AccentError : QhysicsUiStyle.AccentActive);
            }
            if (_hpLabel != null && _vitals != null)
                _hpLabel.text = "HP " + Mathf.CeilToInt(_vitals.Hp) + "/" + Mathf.CeilToInt(_vitals.MaxHp)
                    + (_vitals.HasShield ? "  SHIELD" : "");
            if (_opLabel != null)
            {
                _op = PlayerOperatorController.Instance;
                string name = _op != null ? _op.Current.DisplayName : "Operator";
                _opLabel.text = "TRAINING SYSTEM · " + name;
            }
        }

        void RefreshCarry()
        {
            if (_carry == null)
                _carry = PlayerCarryInventory.Instance;
            if (_slotImages == null || _carry == null)
                return;
            int n = Mathf.Min(_slotImages.Length, _carry.SlotCount);
            for (int i = 0; i < _slotImages.Length; i++)
            {
                if (_slotImages[i] == null)
                    continue;
                bool active = i < _carry.SlotCount;
                _slotImages[i].gameObject.SetActive(active);
                if (!active)
                    continue;
                CarryItemId id = _carry.GetSlot(i);
                bool selected = i == _carry.SelectedIndex;
                bool equipped = i == _carry.EquippedSlot;
                _slotImages[i].color = equipped ? QhysicsUiStyle.TabActive
                    : (selected ? QhysicsUiStyle.ChipBgActive : QhysicsUiStyle.ChipBg);
                if (_slotLabels[i] != null)
                {
                    _slotLabels[i].text = id == CarryItemId.None ? "—" : Short(CarryItemCatalog.LabelOf(id));
                    _slotLabels[i].color = id == CarryItemId.None ? QhysicsUiStyle.TextMuted
                        : (selected ? QhysicsUiStyle.AccentInfo : QhysicsUiStyle.TextPrimary);
                }
            }
        }

        static string Short(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "—";
            if (s.Length <= 10)
                return s;
            return s.Substring(0, 9) + "…";
        }

        static bool WasPrev()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.leftBracketKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.LeftBracket)) return true;
#endif
            return false;
        }

        static bool WasNext()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rightBracketKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.RightBracket)) return true;
#endif
            return false;
        }

        static bool WasUse()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.U)) return true;
#endif
            return false;
        }

        static bool WasDrop()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.X)) return true;
#endif
            return false;
        }
    }
}
