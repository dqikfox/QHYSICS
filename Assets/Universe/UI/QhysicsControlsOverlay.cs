using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.UI
{
    /// <summary>
    /// Full controls reference on demand (replaces the permanent help line). F1 toggles on desktop;
    /// toolbelt WORLD > Controls opens it in XR. Two columns: desktop + VR.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(208)]
    public sealed class QhysicsControlsOverlay : MonoBehaviour
    {
        public const string RootName = "QhysicsControlsOverlay";

        public const string DesktopText =
            "<b><color=#00E5FF>DESKTOP</color></b>\n" +
            "WASD move  |  Mouse look  |  Shift sprint  |  Space jump  |  Ctrl crouch\n" +
            "E / LMB grab  |  F / RMB drop  |  R throw\n" +
            "1-9 dock tools  |  Q prev tool  |  wheel cycle\n" +
            "M / Tab toolbelt  |  C challenges  |  Esc / P pause\n" +
            "[ ] carry slot  |  U use / equip  |  X drop carry\n" +
            "O operator  |  H / J scientist  |  F5 save  |  F9 load\n" +
            "N / P cycle gadget mode  |  F1 this panel\n" +
            "<b><color=#00E5FF>COMBAT</color></b>\n" +
            "E grab weapon  |  LMB slash  |  RMB thrust  |  hold Alt block\n" +
            "F drop  |  R throw (while holding a weapon)\n" +
            "hold V cast  |  Z next spell  |  B imbue weapon\n" +
            "G focus (slow-mo)  |  K skills  |  L arena\n" +
            "F2 dagger  F3 sword  F4 spear  F6 mace  F7 shield\n" +
            "F8 enemy  |  F10 training dummy";

        public const string XrText =
            "<b><color=#00E5FF>VR (Quest)</color></b>\n" +
            "Left stick move  |  Right stick turn / scroll lists\n" +
            "Grip grab / hold  |  Trigger use / activate\n" +
            "Menu / B / Y toolbelt  |  Ray + trigger press UI\n" +
            "Right stick L/R carry slot  |  stick click equip\n" +
            "<b><color=#00E5FF>COMBAT</color></b>\n" +
            "Grip a weapon to hold it (weight + lag)\n" +
            "Second hand on the grip = two-handed\n" +
            "Empty hand: hold trigger = charge, release = cast\n" +
            "(spells work in the arena / near enemies / armed)\n" +
            "A / X next spell  |  A + X together = focus\n" +
            "Full charge next to the blade in your other hand = imbue\n" +
            "Toolbelt COMBAT: weapons, enemy, dummy, skills, arena";

        static QhysicsControlsOverlay _instance;

        QhysicsHudCanvas _hud;
        bool _open;
        Camera _cam;

        public static bool IsOpen => _instance != null && _instance._open;

        public static QhysicsControlsOverlay Ensure(Transform parent)
        {
            if (_instance != null)
                return _instance;
            _instance = UnityEngine.Object.FindAnyObjectByType<QhysicsControlsOverlay>(FindObjectsInactive.Include);
            if (_instance != null)
                return _instance;
            var go = new GameObject(RootName);
            if (parent != null)
                go.transform.SetParent(parent, false);
            _instance = go.AddComponent<QhysicsControlsOverlay>();
            return _instance;
        }

        public static void Toggle()
        {
            Ensure(null).SetOpen(!IsOpen);
        }

        void Awake()
        {
            _instance = this;
            Build();
        }

        void Build()
        {
            _hud = QhysicsHudCanvas.Create("ControlsCanvas", transform, 230);
            _hud.ScreenAnchor = new Vector2(0.5f, 0.5f);
            _hud.ScreenOffset = Vector2.zero;
            _hud.WorldOffset = new Vector3(0f, 0f, 1.3f);
            _hud.WorldSize = new Vector2(1320f, 640f);

            _hud.Root.sizeDelta = new Vector2(1280f, 600f);
            var face = QhysicsUiBuilder.BorderPanel(_hud.Root, "Panel", new Vector2(1280f, 600f));
            var title = QhysicsUiBuilder.Label(face.transform, "Title", "CONTROLS", QhysicsUiStyle.FontBody,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            title.rectTransform.anchoredPosition = new Vector2(-440f, 262f);
            title.rectTransform.sizeDelta = new Vector2(360f, 40f);

            var close = QhysicsUiBuilder.ChipButton(face.transform, "Close", "Close (F1)", new Vector2(180f, 48f), () => SetOpen(false));
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(530f, 262f);

            var left = QhysicsUiBuilder.Label(face.transform, "Desktop", DesktopText, QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.TopLeft);
            left.rectTransform.anchoredPosition = new Vector2(-310f, -30f);
            left.rectTransform.sizeDelta = new Vector2(600f, 500f);
            left.overflowMode = TextOverflowModes.Overflow;
            left.lineSpacing = 8f;

            var right = QhysicsUiBuilder.Label(face.transform, "Xr", XrText, QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.TopLeft);
            right.rectTransform.anchoredPosition = new Vector2(320f, -30f);
            right.rectTransform.sizeDelta = new Vector2(600f, 500f);
            right.overflowMode = TextOverflowModes.Overflow;
            right.lineSpacing = 8f;

            _hud.SetActive(false);
        }

        public void SetOpen(bool on)
        {
            _open = on;
            _hud.SetActive(on);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                SetOpen(!_open);
#endif
        }

        void LateUpdate()
        {
            if (!_open)
                return;
            if (_cam == null || !_cam.isActiveAndEnabled)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            _hud.Tick(_cam, 6f);
        }
    }
}
