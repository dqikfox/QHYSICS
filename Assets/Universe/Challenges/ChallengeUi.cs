using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RealityEngine.UI;

using UiButton = UnityEngine.UI.Button;

namespace RealityEngine.Challenges
{
    /// <summary>
    /// Runtime-built uGUI for the challenge system. Three panels:
    ///  1) Challenge list — toggleable via 'C' key, shows all challenges with lock state, stars, Start button.
    ///  2) Objective overlay — small floating panel visible while a challenge is active.
    ///  3) Completion banner — temporary popup showing stars earned.
    /// Self-spawns via RuntimeInitializeOnLoadMethod. Attaches to the QhysicsUI root when discoverable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class ChallengeUi : MonoBehaviour
    {
        const string RootName = "ChallengeUi";
        const string QhysicsUIRoot = "QhysicsUI";
        const float BannerDuration = 6f;

        Canvas _canvas;
        RectTransform _listPanel;
        RectTransform _listContent;
        RectTransform _overlayPanel;
        TextMeshProUGUI _overlayTitle;
        TextMeshProUGUI _overlayTimer;
        TextMeshProUGUI _overlayObjectives;
        RectTransform _bannerPanel;
        TextMeshProUGUI _bannerTitle;
        TextMeshProUGUI _bannerStars;
        TextMeshProUGUI _bannerStats;

        bool _listVisible;
        bool _uiAttached;
        ChallengeManager _manager;
        Camera _cam;
        float _bannerHideAt;
        float _nextListRefresh;

        /// <summary>Self-spawn after scene load (additive — does not modify QhysicsUiBootstrap).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (!Application.isPlaying)
                return;
            ChallengeUi existing = UnityEngine.Object.FindAnyObjectByType<ChallengeUi>(FindObjectsInactive.Include);
            if (existing != null)
                return;
            var go = new GameObject(RootName);
            go.AddComponent<ChallengeUi>();
        }

        void Awake()
        {
            // Defer UI construction until the QhysicsUI root is found (see Update).
        }

        void Update()
        {
            // Lazy-attach to QhysicsUI root
            if (!_uiAttached)
            {
                Transform root = FindQhysicsUIRoot();
                if (root != null)
                {
                    transform.SetParent(root, false);
                    Build();
                    _uiAttached = true;
                }
                else
                {
                    // Root not yet available — try again next frame
                    return;
                }
            }

            HandleInput();
        }

        void LateUpdate()
        {
            if (!_uiAttached || _canvas == null)
                return;

            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;

            // Position panels
            PositionListPanel();
            PositionOverlay();
            PositionBanner();

            QhysicsUiBuilder.WireEventCamera(_canvas);

            // Refresh overlay and list periodically
            if (Time.unscaledTime >= _nextListRefresh)
            {
                _nextListRefresh = Time.unscaledTime + 0.5f;
                if (_listVisible)
                    RefreshList();
                RefreshOverlay();
            }

            // Auto-hide banner
            if (_bannerPanel != null && _bannerPanel.gameObject.activeSelf && Time.unscaledTime >= _bannerHideAt)
                _bannerPanel.gameObject.SetActive(false);
        }

        // ── UI construction ────────────────────────────────────────

        static Transform FindQhysicsUIRoot()
        {
            GameObject go = GameObject.Find(QhysicsUIRoot);
            if (go != null)
                return go.transform;
            // Fallback: search for QhysicsUiBootstrap host
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<QhysicsUiBootstrap>(FindObjectsInactive.Include);
            if (bootstrap != null)
            {
                Transform child = bootstrap.transform.Find(QhysicsUIRoot);
                if (child != null)
                    return child;
                return bootstrap.transform;
            }
            return null;
        }

        void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(900f, 720f));
            QhysicsUiBuilder.WireEventCamera(_canvas);

            BuildListPanel();
            BuildOverlayPanel();
            BuildBannerPanel();

            // Start hidden
            _canvas.gameObject.SetActive(true);
            _listPanel.gameObject.SetActive(false);
            _overlayPanel.gameObject.SetActive(false);
            _bannerPanel.gameObject.SetActive(false);

            // Subscribe to manager events
            TrySubscribeManager();
        }

        void BuildListPanel()
        {
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "ListPanel", new Vector2(860f, 680f));
            _listPanel = face.rectTransform;
            QhysicsUiBuilder.LayoutVertical(_listPanel, 10f);

            // Header row
            var headerGo = new GameObject("Header", typeof(RectTransform));
            headerGo.transform.SetParent(_listPanel, false);
            var headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.sizeDelta = new Vector2(820f, 60f);
            QhysicsUiBuilder.LayoutHorizontal(headerRt, 8f);

            var title = QhysicsUiBuilder.Label(headerRt, "Title", "CHALLENGES", QhysicsUiStyle.FontTitle,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.sizeDelta = new Vector2(500f, 52f);

            QhysicsUiBuilder.ChipButton(headerRt, "CloseBtn", "Close", new Vector2(120f, 48f),
                () => SetListVisible(false));

            // Scrollable content area
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_listPanel, false);
            _listContent = contentGo.GetComponent<RectTransform>();
            _listContent.sizeDelta = new Vector2(820f, 600f);
            QhysicsUiBuilder.LayoutVertical(_listContent, 8f);

            // Hint label
            var hint = QhysicsUiBuilder.Label(_listPanel, "Hint", "Press C to toggle this panel",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            hint.rectTransform.sizeDelta = new Vector2(820f, 28f);
        }

        void BuildOverlayPanel()
        {
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "OverlayPanel", new Vector2(440f, 280f));
            _overlayPanel = face.rectTransform;
            _overlayPanel.gameObject.SetActive(false);

            _overlayTitle = QhysicsUiBuilder.Label(face.transform, "Title", "Challenge",
                QhysicsUiStyle.FontBody, QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            _overlayTitle.rectTransform.anchoredPosition = new Vector2(0f, 110f);
            _overlayTitle.rectTransform.sizeDelta = new Vector2(420f, 36f);

            _overlayTimer = QhysicsUiBuilder.Label(face.transform, "Timer", "0.0s",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.MidlineRight);
            _overlayTimer.rectTransform.anchoredPosition = new Vector2(0f, 110f);
            _overlayTimer.rectTransform.sizeDelta = new Vector2(100f, 28f);

            _overlayObjectives = QhysicsUiBuilder.Label(face.transform, "Objectives", "",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextPrimary, TextAlignmentOptions.TopLeft);
            _overlayObjectives.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            _overlayObjectives.rectTransform.sizeDelta = new Vector2(420f, 140f);

            QhysicsUiBuilder.ChipButton(face.transform, "CancelBtn", "Abandon",
                new Vector2(140f, 52f), OnAbandonClick);
            // Position cancel button at bottom
            var cancelRt = face.transform.Find("CancelBtn");
            if (cancelRt != null)
            {
                cancelRt.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -110f);
            }
        }

        void BuildBannerPanel()
        {
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "BannerPanel", new Vector2(560f, 320f));
            _bannerPanel = face.rectTransform;
            _bannerPanel.gameObject.SetActive(false);

            _bannerTitle = QhysicsUiBuilder.Label(face.transform, "Title", "CHALLENGE COMPLETE!",
                QhysicsUiStyle.FontTitle, QhysicsUiStyle.AccentActive, TextAlignmentOptions.Center);
            _bannerTitle.rectTransform.anchoredPosition = new Vector2(0f, 110f);
            _bannerTitle.rectTransform.sizeDelta = new Vector2(520f, 52f);

            _bannerStars = QhysicsUiBuilder.Label(face.transform, "Stars", "",
                56f, QhysicsUiStyle.AccentAttention, TextAlignmentOptions.Center);
            _bannerStars.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            _bannerStars.rectTransform.sizeDelta = new Vector2(520f, 72f);

            _bannerStats = QhysicsUiBuilder.Label(face.transform, "Stats", "",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            _bannerStats.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            _bannerStats.rectTransform.sizeDelta = new Vector2(520f, 40f);

            QhysicsUiBuilder.ChipButton(face.transform, "ContinueBtn", "Continue",
                new Vector2(200f, 56f), OnContinueClick);
            var continueRt = face.transform.Find("ContinueBtn");
            if (continueRt != null)
            {
                continueRt.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -100f);
            }
        }

        // ── Manager subscription ───────────────────────────────────

        void TrySubscribeManager()
        {
            if (_manager != null)
                return;
            _manager = ChallengeManager.Instance;
            if (_manager == null)
                _manager = UnityEngine.Object.FindAnyObjectByType<ChallengeManager>(FindObjectsInactive.Include);
            if (_manager == null)
                return;

            _manager.OnObjectiveCompleted += OnObjectiveCompleted;
            _manager.OnChallengeCompleted += OnChallengeCompleted;
            _manager.OnChallengeAbandoned += OnChallengeAbandoned;
        }

        void OnObjectiveCompleted(ChallengeObjective obj, int index)
        {
            RefreshOverlay();
        }

        void OnChallengeCompleted(ChallengeDefinition def, int stars, int componentCount)
        {
            RefreshList();
            ShowBanner(def, stars, componentCount);
            _overlayPanel.gameObject.SetActive(false);
        }

        void OnChallengeAbandoned()
        {
            _overlayPanel.gameObject.SetActive(false);
        }

        // ── Input ──────────────────────────────────────────────────

        void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame)
                ToggleList();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.C))
                ToggleList();
#endif
        }

        public void ToggleList()
        {
            SetListVisible(!_listVisible);
        }

        void SetListVisible(bool on)
        {
            _listVisible = on;
            if (_listPanel != null)
                _listPanel.gameObject.SetActive(on);
            if (on)
            {
                RefreshList();
            }
        }

        // ── List refresh ───────────────────────────────────────────

        void RefreshList()
        {
            if (_manager == null || _listContent == null)
                return;

            // Clear existing entries
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                Transform c = _listContent.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            ChallengeDefinition[] campaign = _manager.Campaign;
            if (campaign == null)
                return;

            for (int i = 0; i < campaign.Length; i++)
            {
                BuildListEntry(campaign[i], i);
            }
        }

        void BuildListEntry(ChallengeDefinition def, int index)
        {
            bool unlocked = _manager.IsUnlocked(def.id);
            int stars = _manager.GetStars(def.id);
            float bestTime = _manager.GetBestTime(def.id);
            int bestComp = _manager.GetBestComponentCount(def.id);

            // Entry container
            var entryGo = new GameObject("Entry_" + def.id, typeof(RectTransform));
            entryGo.transform.SetParent(_listContent, false);
            var entryRt = entryGo.GetComponent<RectTransform>();
            entryRt.sizeDelta = new Vector2(820f, 90f);

            // Background
            Color bgColor = unlocked ? QhysicsUiStyle.ChipBg : QhysicsUiStyle.TabIdle;
            var bg = QhysicsUiBuilder.Panel(entryRt, "Bg", bgColor, new Vector2(820f, 86f));
            bg.raycastTarget = false;

            // Title
            Color titleColor = unlocked ? QhysicsUiStyle.TextPrimary : QhysicsUiStyle.TextMuted;
            var title = QhysicsUiBuilder.Label(bg.transform, "Title", def.title,
                QhysicsUiStyle.FontBody, titleColor, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.anchoredPosition = new Vector2(-380f, 22f);
            title.rectTransform.sizeDelta = new Vector2(360f, 34f);

            // Stars
            string starText = FormatStars(stars);
            Color starColor = stars > 0 ? QhysicsUiStyle.AccentAttention : QhysicsUiStyle.TextMuted;
            var starsLabel = QhysicsUiBuilder.Label(bg.transform, "Stars", starText,
                QhysicsUiStyle.FontBody, starColor, TextAlignmentOptions.MidlineLeft);
            starsLabel.rectTransform.anchoredPosition = new Vector2(-380f, -16f);
            starsLabel.rectTransform.sizeDelta = new Vector2(200f, 30f);

            // Best time / components
            if (bestTime > 0f)
            {
                string stats = "Best: " + bestTime.ToString("0.0") + "s";
                if (bestComp > 0)
                    stats += " | " + bestComp + " parts";
                var statsLabel = QhysicsUiBuilder.Label(bg.transform, "Stats", stats,
                    QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.MidlineRight);
                statsLabel.rectTransform.anchoredPosition = new Vector2(200f, -16f);
                statsLabel.rectTransform.sizeDelta = new Vector2(200f, 28f);
            }

            // Button: Start or Locked
            if (unlocked)
            {
                string btnLabel = stars > 0 ? "Replay" : "Start";
                var btn = QhysicsUiBuilder.ChipButton(bg.transform, "StartBtn", btnLabel,
                    new Vector2(140f, 60f), () => OnStartClick(def.id));
                var btnRt = btn.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(1f, 0.5f);
                btnRt.anchorMax = new Vector2(1f, 0.5f);
                btnRt.anchoredPosition = new Vector2(-80f, 0f);
            }
            else
            {
                var lockLabel = QhysicsUiBuilder.Label(bg.transform, "Locked", "LOCKED",
                    QhysicsUiStyle.FontBody, QhysicsUiStyle.AccentError, TextAlignmentOptions.Center);
                lockLabel.rectTransform.anchorMin = new Vector2(1f, 0.5f);
                lockLabel.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                lockLabel.rectTransform.anchoredPosition = new Vector2(-80f, 0f);
                lockLabel.rectTransform.sizeDelta = new Vector2(140f, 40f);
            }
        }

        static string FormatStars(int stars)
        {
            switch (stars)
            {
                case 0: return "\u2606\u2606\u2606"; // ☆☆☆
                case 1: return "\u2605\u2606\u2606"; // ★☆☆
                case 2: return "\u2605\u2605\u2606"; // ★★☆
                default: return "\u2605\u2605\u2605"; // ★★★
            }
        }

        void OnStartClick(string challengeId)
        {
            if (_manager == null)
                return;
            if (_manager.StartChallenge(challengeId))
            {
                SetListVisible(false);
                _overlayPanel.gameObject.SetActive(true);
                RefreshOverlay();
            }
        }

        void OnAbandonClick()
        {
            if (_manager != null)
                _manager.AbandonChallenge();
        }

        void OnContinueClick()
        {
            if (_bannerPanel != null)
                _bannerPanel.gameObject.SetActive(false);
            // Show the list again so the player can pick the next challenge
            SetListVisible(true);
        }

        // ── Overlay refresh ────────────────────────────────────────

        void RefreshOverlay()
        {
            if (_manager == null || _overlayPanel == null)
                return;

            if (!_manager.IsChallengeActive)
            {
                _overlayPanel.gameObject.SetActive(false);
                return;
            }

            _overlayPanel.gameObject.SetActive(true);

            if (_overlayTitle != null && _manager.ActiveChallenge != null)
                _overlayTitle.text = _manager.ActiveChallenge.title;

            if (_overlayTimer != null)
                _overlayTimer.text = _manager.ElapsedTime.ToString("0.0") + "s";

            if (_overlayObjectives != null && _manager.ActiveObjectives != null)
            {
                var sb = new System.Text.StringBuilder(256);
                for (int i = 0; i < _manager.ActiveObjectives.Length; i++)
                {
                    ChallengeObjective obj = _manager.ActiveObjectives[i];
                    sb.Append(obj.completed ? "\u2713 " : "\u25CB "); // ✓ or ○
                    sb.Append(obj.displayText);
                    if (i < _manager.ActiveObjectives.Length - 1)
                        sb.Append("\n");
                }
                _overlayObjectives.text = sb.ToString();
            }
        }

        // ── Banner ─────────────────────────────────────────────────

        void ShowBanner(ChallengeDefinition def, int stars, int componentCount)
        {
            if (_bannerPanel == null)
                return;
            _bannerPanel.gameObject.SetActive(true);
            _bannerHideAt = Time.unscaledTime + BannerDuration;

            if (_bannerTitle != null)
                _bannerTitle.text = def != null ? def.title.ToUpper() + " COMPLETE!" : "COMPLETE!";

            if (_bannerStars != null)
                _bannerStars.text = FormatStars(stars);

            if (_bannerStats != null)
            {
                string stats = "Time: " + _manager.ElapsedTime.ToString("0.0") + "s";
                if (componentCount > 0)
                    stats += "  |  Parts: " + componentCount;
                _bannerStats.text = stats;
            }
        }

        // ── Positioning ────────────────────────────────────────────

        void PositionListPanel()
        {
            if (!_listVisible || _listPanel == null)
                return;
            // Center in front of camera, like QhysicsMainMenu
            Vector3 fwd = Flatten(_cam.transform.forward);
            Vector3 pos = _cam.transform.position + fwd * 1.2f + Vector3.up * 0.1f;
            _listPanel.root.transform.position = Vector3.Lerp(
                _listPanel.root.transform.position, pos,
                1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(_listPanel.root, _cam);
        }

        void PositionOverlay()
        {
            if (_overlayPanel == null || !_overlayPanel.gameObject.activeInHierarchy)
                return;
            // Small panel to the left of center, slightly above eye level
            Vector3 fwd = Flatten(_cam.transform.forward);
            Vector3 right = Flatten(_cam.transform.right);
            Vector3 pos = _cam.transform.position + fwd * 0.7f + right * -0.3f + Vector3.up * 0.15f;
            _overlayPanel.root.transform.position = Vector3.Lerp(
                _overlayPanel.root.transform.position, pos,
                1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(_overlayPanel.root, _cam);
        }

        void PositionBanner()
        {
            if (_bannerPanel == null || !_bannerPanel.gameObject.activeInHierarchy)
                return;
            // Centered, slightly above center
            Vector3 fwd = Flatten(_cam.transform.forward);
            Vector3 pos = _cam.transform.position + fwd * 1.0f + Vector3.up * 0.2f;
            _bannerPanel.root.transform.position = Vector3.Lerp(
                _bannerPanel.root.transform.position, pos,
                1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(_bannerPanel.root, _cam);
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }
}
