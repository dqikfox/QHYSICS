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
    /// Dark-glass Operator Select (cyan #00E5FF). Toggle with O / from Pause.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(205)]
    public sealed class QhysicsOperatorSelectPanel : MonoBehaviour
    {
        public const string RootName = "QhysicsOperatorSelectPanel";

        Canvas _canvas;
        Camera _cam;
        bool _open;
        TextMeshProUGUI _detail;

        public bool IsOpen => _open;

        public static QhysicsOperatorSelectPanel Ensure(Transform parent)
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
                var c = existing.GetComponent<QhysicsOperatorSelectPanel>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsOperatorSelectPanel>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsOperatorSelectPanel>();
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

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(860f, 620f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(820f, 580f));
            QhysicsUiBuilder.LayoutVertical(face.rectTransform, 12f);

            var title = QhysicsUiBuilder.Label(face.transform, "Title", "OPERATOR SELECT", QhysicsUiStyle.FontTitle,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.Center);
            title.rectTransform.sizeDelta = new Vector2(760f, 48f);

            var sub = QhysicsUiBuilder.Label(face.transform, "Sub", "Lab training archetypes — kit + locomotion", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            sub.rectTransform.sizeDelta = new Vector2(760f, 28f);

            for (int i = 0; i < OperatorArchetype.Catalog.Length; i++)
            {
                int idx = i;
                var arch = OperatorArchetype.Catalog[i];
                string label = arch.DisplayName + "  ·  slots " + arch.InventorySlots + "  ·  HP " + arch.MaxHp;
                QhysicsUiBuilder.ChipButton(face.transform, "Op_" + arch.Id, label, new Vector2(760f, 72f), () => Select(idx));
            }

            _detail = QhysicsUiBuilder.Label(face.transform, "Detail", "", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.Center);
            _detail.rectTransform.sizeDelta = new Vector2(760f, 64f);

            QhysicsUiBuilder.ChipButton(face.transform, "Close", "Close", new Vector2(320f, 72f), () => SetOpen(false));
            RefreshDetail();
            SetOpen(false);
        }

        void Update()
        {
            if (WasTogglePressed())
                Toggle();
        }

        void LateUpdate()
        {
            if (!_open)
                return;
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;
            Vector3 fwd = _cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f)
                fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 pos = _cam.transform.position + fwd * 1.1f + Vector3.up * 0.05f;
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
        }

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool on)
        {
            _open = on;
            if (_canvas != null)
                _canvas.gameObject.SetActive(on);
            if (on)
            {
                RefreshDetail();
                if (Time.timeScale > 0.01f)
                    Time.timeScale = 0f;
            }
            else if (Time.timeScale < 0.01f)
            {
                // Don't unpause if pause panel still open
                var pause = Object.FindFirstObjectByType<QhysicsPausePanel>(FindObjectsInactive.Include);
                if (pause == null || !pause.IsOpen)
                    Time.timeScale = 1f;
            }
        }

        void Select(int index)
        {
            var op = PlayerOperatorController.Ensure();
            op.SelectByIndex(index, refillKit: true);
            RefreshDetail();
        }

        void RefreshDetail()
        {
            if (_detail == null)
                return;
            var op = PlayerOperatorController.Instance;
            OperatorArchetype arch = op != null ? op.Current : OperatorArchetype.Catalog[0];
            _detail.text = arch.DisplayName + " — " + arch.Blurb + "\nMove " + arch.MoveSpeed.ToString("0.0")
                + "  Sprint x" + arch.SprintMult.ToString("0.00") + "  (saved)";
        }

        static bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.O))
                return true;
#endif
            return false;
        }
    }
}
