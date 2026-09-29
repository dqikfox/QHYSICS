using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RealityEngine.Challenges;

namespace RealityEngine.UI
{
    /// <summary>
    /// Level intro card: "CHAPTER 1 | LEVEL n/10", level title and a one-line goal. Fades in, holds, fades out.
    /// Desktop: screen-space upper centre. XR: world-space ~1.3 m ahead, head-follow (QhysicsHudCanvas).
    /// Shown automatically whenever a challenge starts (menu, Continue or the challenge list); Show() for custom cards.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(207)]
    public sealed class QhysicsLevelIntroCard : MonoBehaviour
    {
        const float FadeIn = 0.45f;
        const float Hold = 4.0f;
        const float FadeOut = 0.9f;

        static QhysicsLevelIntroCard _instance;

        QhysicsHudCanvas _hud;
        CanvasGroup _group;
        TextMeshProUGUI _kicker;
        TextMeshProUGUI _title;
        TextMeshProUGUI _goal;
        Camera _cam;
        float _shownAt = -99f;
        bool _showing;
        string _lastActiveId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying)
                return;
            Ensure();
        }

        public static QhysicsLevelIntroCard Ensure()
        {
            if (_instance != null)
                return _instance;
            _instance = UnityEngine.Object.FindAnyObjectByType<QhysicsLevelIntroCard>(FindObjectsInactive.Include);
            if (_instance != null)
                return _instance;
            var go = new GameObject("QhysicsLevelIntroCard");
            _instance = go.AddComponent<QhysicsLevelIntroCard>();
            return _instance;
        }

        public static void Show(string kicker, string title, string goal)
        {
            Ensure().ShowInternal(kicker, title, goal);
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
            _hud = QhysicsHudCanvas.Create("IntroCanvas", transform, 210);
            _hud.ScreenAnchor = new Vector2(0.5f, 0.72f);
            _hud.ScreenOffset = Vector2.zero;
            _hud.WorldOffset = new Vector3(0f, 0.08f, 1.3f);
            _hud.WorldSize = new Vector2(1100f, 300f);
            _hud.WorldScale = 1.1f;
            _group = _hud.Canvas.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Vector2 size = new Vector2(1000f, 230f);
            _hud.Root.sizeDelta = size;
            Image bg = QhysicsUiBuilder.Panel(_hud.Root, "Card", QhysicsUiStyle.PanelBg, size);
            bg.raycastTarget = false;
            Image accent = QhysicsUiBuilder.Panel(bg.transform, "Accent", QhysicsUiStyle.AccentInfo, new Vector2(560f, 4f));
            accent.rectTransform.anchorMin = accent.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            accent.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            accent.raycastTarget = false;

            _kicker = Line(bg.transform, "Kicker", QhysicsUiStyle.FontSmall, QhysicsUiStyle.AccentInfo, 70f, 34f);
            _kicker.characterSpacing = 6f;
            _title = Line(bg.transform, "Title", 50f, QhysicsUiStyle.TextPrimary, 0f + 18f, 64f);
            _title.fontStyle = FontStyles.Bold;
            _goal = Line(bg.transform, "Goal", QhysicsUiStyle.FontBody, QhysicsUiStyle.TextMuted, -62f, 40f);
            _hud.SetActive(false);
        }

        static TextMeshProUGUI Line(Transform parent, string name, float size, Color color, float y, float h)
        {
            TextMeshProUGUI t = QhysicsUiBuilder.Label(parent, name, "", size, color, TextAlignmentOptions.Center);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(960f, h);
            t.rectTransform.anchoredPosition = new Vector2(0f, y);
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        void ShowInternal(string kicker, string title, string goal)
        {
            if (_hud == null)
                Build();
            _kicker.text = kicker ?? string.Empty;
            _title.text = title ?? string.Empty;
            _goal.text = goal ?? string.Empty;
            _shownAt = Time.unscaledTime;
            _showing = true;
            _group.alpha = 0f;
            _hud.SetActive(true);
        }

        void Update()
        {
            ChallengeManager mgr = ChallengeManager.Instance;
            string active = mgr != null && mgr.ActiveChallenge != null ? mgr.ActiveChallenge.id : null;
            if (active != _lastActiveId)
            {
                _lastActiveId = active;
                if (active != null)
                    ShowForChallenge(mgr.ActiveChallenge);
            }
        }

        void ShowForChallenge(ChallengeDefinition def)
        {
            string kicker;
            ChapterDefinition ch = ChapterCatalog.GetChapterFor(def.id);
            if (ch != null && ch.id == ChapterCatalog.Chapter1Id)
                kicker = "CHAPTER 1  |  LEVEL " + (ch.IndexOf(def.id) + 1) + " / " + ch.challengeIds.Length;
            else
                kicker = "SANDBOX / EXTRA";
            ShowInternal(kicker, def.title, GoalLine(def));
        }

        /// <summary>One-line goal: first objective (+N more), else the first sentence of the description.</summary>
        public static string GoalLine(ChallengeDefinition def)
        {
            if (def == null)
                return string.Empty;
            if (def.objectives != null && def.objectives.Length > 0 && def.objectives[0] != null)
            {
                string g = "Goal: " + def.objectives[0].displayText;
                if (def.objectives.Length > 1)
                    g += "  (+" + (def.objectives.Length - 1) + " more)";
                return g;
            }
            string d = def.description ?? string.Empty;
            int dot = d.IndexOf(". ");
            return dot > 0 ? d.Substring(0, dot + 1) : d;
        }

        void LateUpdate()
        {
            if (!_showing || _hud == null || _hud.Canvas == null)
                return;
            if (QhysicsUiState.BootMenuOpen)
            {
                // Hold the card until the menu closes (e.g. level started from the menu).
                _shownAt = Time.unscaledTime;
                _hud.SetActive(false);
                return;
            }
            _hud.SetActive(true);
            if (_cam == null || !_cam.isActiveAndEnabled)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            _hud.Tick(_cam, 6f);

            float t = Time.unscaledTime - _shownAt;
            float a;
            if (t < FadeIn) a = t / FadeIn;
            else if (t < FadeIn + Hold) a = 1f;
            else a = 1f - (t - FadeIn - Hold) / FadeOut;
            _group.alpha = Mathf.Clamp01(a);
            if (t >= FadeIn + Hold + FadeOut)
            {
                _showing = false;
                _hud.SetActive(false);
            }
        }
    }
}
