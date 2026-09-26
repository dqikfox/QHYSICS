using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RealityEngine.UI
{
    /// <summary>
    /// Single transient hint card (never more than one visible). Show() replaces the current text and
    /// restarts the timer; the card fades out after its duration. Desktop: screen-space above the dock.
    /// XR: world-space just below eye line.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(206)]
    public sealed class QhysicsHintCard : MonoBehaviour
    {
        public const string RootName = "QhysicsHintCard";
        const float FadeSeconds = 0.6f;

        static QhysicsHintCard _instance;
        static bool _firstRunShown;

        QhysicsHudCanvas _hud;
        CanvasGroup _group;
        TextMeshProUGUI _text;
        Image _bg;
        float _hideAt;
        float _shownAt;
        Camera _cam;

        public static QhysicsHintCard Ensure(Transform parent)
        {
            if (_instance != null)
                return _instance;
            _instance = UnityEngine.Object.FindAnyObjectByType<QhysicsHintCard>(FindObjectsInactive.Include);
            if (_instance != null)
                return _instance;
            var go = new GameObject(RootName);
            if (parent != null)
                go.transform.SetParent(parent, false);
            _instance = go.AddComponent<QhysicsHintCard>();
            return _instance;
        }

        /// <summary>Show a hint (replaces any visible hint). Duration in real seconds.</summary>
        public static void Show(string text, float seconds = 6f)
        {
            QhysicsHintCard c = Ensure(null);
            c.ShowInternal(text, seconds);
        }

        /// <summary>First-run controls hint, once per Play session.</summary>
        public static void ShowFirstRunOnce()
        {
            if (_firstRunShown)
                return;
            _firstRunShown = true;
            if (QhysicsUiState.IsXr)
                Show("Grip grab  |  Trigger use  |  Menu / B / Y toolbelt  |  Toolbelt WORLD > Controls for the full list", 6f);
            else
                Show("WASD move  |  Mouse look  |  E grab  |  M / Tab toolbelt  |  C challenges  |  F1 controls", 6f);
        }

        void Awake()
        {
            _instance = this;
            Build();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void Build()
        {
            _hud = QhysicsHudCanvas.Create("HintCanvas", transform, 205);
            _hud.ScreenAnchor = new Vector2(0.5f, 0f);
            _hud.ScreenOffset = new Vector2(0f, 150f);
            _hud.WorldOffset = new Vector3(0f, -0.22f, 0.9f);
            _hud.WorldSize = new Vector2(1000f, 90f);
            _group = _hud.Canvas.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _hud.Root.sizeDelta = new Vector2(1000f, 64f);
            _bg = QhysicsUiBuilder.Panel(_hud.Root, "Card", QhysicsUiStyle.PanelBg, new Vector2(1000f, 64f));
            _bg.raycastTarget = false;
            var accent = QhysicsUiBuilder.Panel(_bg.transform, "Accent", QhysicsUiStyle.AccentInfo, new Vector2(4f, 40f));
            accent.rectTransform.anchorMin = accent.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            accent.rectTransform.anchoredPosition = new Vector2(QhysicsUiStyle.Space2, 0f);
            accent.raycastTarget = false;
            _text = QhysicsUiBuilder.Label(_bg.transform, "Text", "", QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextPrimary,
                TextAlignmentOptions.Center);
            _text.rectTransform.anchorMin = Vector2.zero;
            _text.rectTransform.anchorMax = Vector2.one;
            _text.rectTransform.offsetMin = new Vector2(32f, 4f);
            _text.rectTransform.offsetMax = new Vector2(-16f, -4f);
            _text.overflowMode = TextOverflowModes.Overflow;
            _hud.SetActive(false);
        }

        void ShowInternal(string text, float seconds)
        {
            if (_text == null)
                Build();
            _text.text = text ?? string.Empty;
            // Size the card to the text (no truncation).
            float w = Mathf.Clamp(_text.GetPreferredValues(_text.text, 2000f, 60f).x + 64f, 320f, 1500f);
            _bg.rectTransform.sizeDelta = new Vector2(w, 64f);
            _hud.Root.sizeDelta = new Vector2(w, 64f);
            _shownAt = Time.unscaledTime;
            _hideAt = Time.unscaledTime + Mathf.Max(1f, seconds);
            _group.alpha = 0f;
            _hud.SetActive(QhysicsUiState.GameplayHudVisible);
        }

        void LateUpdate()
        {
            if (_hud == null || _hud.Canvas == null || !_hud.Canvas.gameObject.activeSelf)
                return;
            if (!QhysicsUiState.GameplayHudVisible)
            {
                _hud.SetActive(false);
                return;
            }
            if (_cam == null || !_cam.isActiveAndEnabled)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            _hud.Tick(_cam);

            float now = Time.unscaledTime;
            float fadeIn = Mathf.Clamp01((now - _shownAt) / 0.2f);
            float fadeOut = Mathf.Clamp01((_hideAt - now) / FadeSeconds);
            _group.alpha = Mathf.Min(fadeIn, fadeOut);
            if (now >= _hideAt)
                _hud.SetActive(false);
        }
    }
}
