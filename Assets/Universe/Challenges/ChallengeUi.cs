using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using TMPro;
using RealityEngine.UI;
using RealityEngine.Audio;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using UiButton = UnityEngine.UI.Button;
using XrInputDevice = UnityEngine.XR.InputDevice;
using XrCommonUsages = UnityEngine.XR.CommonUsages;

namespace RealityEngine.Challenges
{
    /// <summary>
    /// Runtime-built uGUI for the challenge system. Three independent world canvases:
    ///  1) Challenge list — chapter tabs + scrollable entries (XR ray drag, right thumbstick, mouse wheel, UP/DOWN buttons).
    ///     Toggle: C key, toolbelt EXPERIMENTS "Challenges" chip, pause menu "Challenges".
    ///  2) Objective overlay — small floating panel while a challenge is active.
    ///  3) Completion banner — stars earned; "CHAPTER COMPLETE" when a chapter's last star lands.
    /// Each canvas is positioned on its own (the old version moved the whole QhysicsUI root). Self-spawns.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class ChallengeUi : MonoBehaviour
    {
        const string RootName = "ChallengeUi";
        const string GateOwner = "ChallengeList";
        const float BannerDuration = 6f;
        const float EntryHeight = 92f;
        const float ListWidth = 860f;
        const float ViewportHeight = 560f;

        public static ChallengeUi Instance { get; private set; }

        /// <summary>True while the challenge list is visible.</summary>
        public static bool IsListOpen => Instance != null && Instance._listVisible;

        Canvas _listCanvas;
        Canvas _overlayCanvas;
        Canvas _bannerCanvas;
        RectTransform _listPanel;
        RectTransform _listContent;
        ScrollRect _scroll;
        TextMeshProUGUI _chapterSubtitle;
        readonly List<UiButton> _chapterTabs = new List<UiButton>();
        RectTransform _overlayPanel;
        TextMeshProUGUI _overlayTitle;
        TextMeshProUGUI _overlayTimer;
        TextMeshProUGUI _overlayObjectives;
        RectTransform _bannerPanel;
        TextMeshProUGUI _bannerTitle;
        TextMeshProUGUI _bannerStars;
        TextMeshProUGUI _bannerStats;

        bool _built;
        bool _listVisible;
        bool _listDirty = true;
        bool _snapList;
        int _chapterIndex;
        ChallengeManager _manager;
        Camera _cam;
        float _bannerHideAt;
        float _nextRefresh;
        readonly HashSet<string> _completeChapters = new HashSet<string>();

        /// <summary>Self-spawn after scene load.</summary>
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

        /// <summary>Toggle the list from anywhere (toolbelt chip, pause menu).</summary>
        public static void ToggleListStatic()
        {
            if (Instance == null)
                AutoSpawn();
            if (Instance != null)
                Instance.ToggleList();
        }

        public static void OpenListStatic()
        {
            if (Instance == null)
                AutoSpawn();
            if (Instance != null)
                Instance.SetListVisible(true);
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            QhysicsUiScrollGate.Set(GateOwner, false);
            if (_manager != null)
            {
                _manager.OnObjectiveCompleted -= OnObjectiveCompleted;
                _manager.OnChallengeCompleted -= OnChallengeCompleted;
                _manager.OnChallengeAbandoned -= OnChallengeAbandoned;
            }
        }

        void Update()
        {
            if (!_built)
            {
                if (QhysicsUiBuilder.ResolveXrCamera() == null)
                    return;
                Build();
                _built = true;
            }
            if (_manager == null)
                TrySubscribeManager();

            HandleInput();
            if (_listVisible)
                HandleScrollInput();
        }

        void LateUpdate()
        {
            if (!_built)
                return;

            if (_cam == null || !_cam.isActiveAndEnabled)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;

            PositionListPanel();
            PositionOverlay();
            PositionBanner();

            if (Time.unscaledTime >= _nextRefresh)
            {
                bool challengeActive = _manager != null && _manager.IsChallengeActive;
                _nextRefresh = Time.unscaledTime + (challengeActive ? 0.2f : 0.5f);
                if (_listVisible && _listDirty)
                    RefreshList();
                RefreshOverlay();
            }

            if (_bannerCanvas != null && _bannerCanvas.gameObject.activeSelf && Time.unscaledTime >= _bannerHideAt)
                _bannerCanvas.gameObject.SetActive(false);
        }

        // ── UI construction ────────────────────────────────────────

        void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _listCanvas = QhysicsUiBuilder.CreateWorldCanvas("ListCanvas", transform, new Vector2(ListWidth + 40f, 820f));
            _overlayCanvas = QhysicsUiBuilder.CreateWorldCanvas("OverlayCanvas", transform, new Vector2(480f, 360f));
            _bannerCanvas = QhysicsUiBuilder.CreateWorldCanvas("BannerCanvas", transform, new Vector2(600f, 360f));
            _listCanvas.sortingOrder = 90;
            _bannerCanvas.sortingOrder = 95;

            BuildListPanel();
            BuildOverlayPanel();
            BuildBannerPanel();

            _listCanvas.gameObject.SetActive(false);
            _overlayCanvas.gameObject.SetActive(false);
            _bannerCanvas.gameObject.SetActive(false);

            TrySubscribeManager();
        }

        void BuildListPanel()
        {
            var face = QhysicsUiBuilder.BorderPanel(_listCanvas.transform, "ListPanel", new Vector2(ListWidth, 800f));
            _listPanel = face.rectTransform;

            // Header row: title + page + close
            var header = NewRect("Header", _listPanel, new Vector2(ListWidth - 40f, 60f));
            header.anchoredPosition = new Vector2(0f, 360f);
            QhysicsUiBuilder.LayoutHorizontal(header, 8f);
            var title = QhysicsUiBuilder.Label(header, "Title", "CHALLENGES", QhysicsUiStyle.FontTitle,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.sizeDelta = new Vector2(430f, 52f);
            QhysicsUiBuilder.ChipButton(header, "UpBtn", "UP", new Vector2(100f, 52f), () => Page(+1));
            QhysicsUiBuilder.ChipButton(header, "DownBtn", "DOWN", new Vector2(110f, 52f), () => Page(-1));
            QhysicsUiBuilder.ChipButton(header, "CloseBtn", "Close", new Vector2(110f, 52f), () => SetListVisible(false));

            // Chapter tabs
            var tabs = NewRect("ChapterTabs", _listPanel, new Vector2(ListWidth - 40f, 58f));
            tabs.anchoredPosition = new Vector2(0f, 296f);
            QhysicsUiBuilder.LayoutHorizontal(tabs, 8f);
            _chapterTabs.Clear();
            ChapterDefinition[] chapters = ChapterCatalog.All;
            for (int i = 0; i < chapters.Length; i++)
            {
                int idx = i;
                string label = chapters[i].order == 1 ? "Ch 1: Faraday's Bench" : chapters[i].title;
                var tab = QhysicsUiBuilder.ChipButton(tabs, "Tab_" + chapters[i].id, label, new Vector2(i == 0 ? 400f : 360f, 50f),
                    () => SelectChapter(idx));
                _chapterTabs.Add(tab);
            }

            _chapterSubtitle = QhysicsUiBuilder.Label(_listPanel, "ChapterSubtitle", "", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextMuted, TextAlignmentOptions.MidlineLeft);
            _chapterSubtitle.rectTransform.anchoredPosition = new Vector2(0f, 248f);
            _chapterSubtitle.rectTransform.sizeDelta = new Vector2(ListWidth - 60f, 32f);

            // Scroll view: viewport (RectMask2D + raycast image) → content (VerticalLayout + ContentSizeFitter)
            var viewportImg = QhysicsUiBuilder.Panel(_listPanel, "Viewport", new Color(0f, 0f, 0f, 0.18f),
                new Vector2(ListWidth - 30f, ViewportHeight));
            RectTransform viewport = viewportImg.rectTransform;
            viewport.anchoredPosition = new Vector2(0f, -40f);
            viewportImg.gameObject.AddComponent<RectMask2D>();

            _listContent = NewRect("Content", viewport, new Vector2(ListWidth - 30f, ViewportHeight));
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = new Vector2(0f, ViewportHeight);
            QhysicsUiBuilder.LayoutVertical(_listContent, 8f);
            var vlg = _listContent.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            var fitter = _listContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            _scroll = viewportImg.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _listContent;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = true;
            _scroll.decelerationRate = 0.12f;
            _scroll.scrollSensitivity = 40f;

            var hint = QhysicsUiBuilder.Label(_listPanel, "Hint",
                "C / toolbelt EXPERIMENTS > Challenges / pause menu.  Scroll: ray-drag, right stick, mouse wheel, UP/DOWN.",
                QhysicsUiStyle.FontSmall - 2f, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -360f);
            hint.rectTransform.sizeDelta = new Vector2(ListWidth - 40f, 56f);
            hint.textWrappingMode = TextWrappingModes.Normal;
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            return rt;
        }

        void BuildOverlayPanel()
        {
            var face = QhysicsUiBuilder.BorderPanel(_overlayCanvas.transform, "OverlayPanel", new Vector2(460f, 340f));
            _overlayPanel = face.rectTransform;

            _overlayTitle = QhysicsUiBuilder.Label(face.transform, "Title", "Challenge",
                QhysicsUiStyle.FontBody, QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            _overlayTitle.rectTransform.anchoredPosition = new Vector2(-40f, 135f);
            _overlayTitle.rectTransform.sizeDelta = new Vector2(360f, 36f);

            _overlayTimer = QhysicsUiBuilder.Label(face.transform, "Timer", "0.0s",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.MidlineRight);
            _overlayTimer.rectTransform.anchoredPosition = new Vector2(170f, 135f);
            _overlayTimer.rectTransform.sizeDelta = new Vector2(100f, 28f);

            _overlayObjectives = QhysicsUiBuilder.Label(face.transform, "Objectives", "",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextPrimary, TextAlignmentOptions.TopLeft);
            _overlayObjectives.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            _overlayObjectives.rectTransform.sizeDelta = new Vector2(440f, 220f);
            _overlayObjectives.textWrappingMode = TextWrappingModes.Normal;
            _overlayObjectives.enableAutoSizing = true;
            _overlayObjectives.fontSizeMin = 14f;
            _overlayObjectives.fontSizeMax = QhysicsUiStyle.FontSmall;

            var abandon = QhysicsUiBuilder.ChipButton(face.transform, "CancelBtn", "Abandon", new Vector2(140f, 52f), OnAbandonClick);
            abandon.GetComponent<RectTransform>().anchoredPosition = new Vector2(-80f, -135f);
            var list = QhysicsUiBuilder.ChipButton(face.transform, "ListBtn", "List", new Vector2(120f, 52f), () => SetListVisible(true));
            list.GetComponent<RectTransform>().anchoredPosition = new Vector2(80f, -135f);
        }

        void BuildBannerPanel()
        {
            var face = QhysicsUiBuilder.BorderPanel(_bannerCanvas.transform, "BannerPanel", new Vector2(580f, 340f));
            _bannerPanel = face.rectTransform;

            _bannerTitle = QhysicsUiBuilder.Label(face.transform, "Title", "CHALLENGE COMPLETE!",
                QhysicsUiStyle.FontTitle, QhysicsUiStyle.AccentActive, TextAlignmentOptions.Center);
            _bannerTitle.rectTransform.anchoredPosition = new Vector2(0f, 115f);
            _bannerTitle.rectTransform.sizeDelta = new Vector2(540f, 56f);
            _bannerTitle.enableAutoSizing = true;
            _bannerTitle.fontSizeMin = 22f;
            _bannerTitle.fontSizeMax = QhysicsUiStyle.FontTitle;

            _bannerStars = QhysicsUiBuilder.Label(face.transform, "Stars", "",
                56f, QhysicsUiStyle.AccentAttention, TextAlignmentOptions.Center);
            _bannerStars.rectTransform.anchoredPosition = new Vector2(0f, 45f);
            _bannerStars.rectTransform.sizeDelta = new Vector2(540f, 72f);

            _bannerStats = QhysicsUiBuilder.Label(face.transform, "Stats", "",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            _bannerStats.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            _bannerStats.rectTransform.sizeDelta = new Vector2(540f, 70f);
            _bannerStats.textWrappingMode = TextWrappingModes.Normal;

            var cont = QhysicsUiBuilder.ChipButton(face.transform, "ContinueBtn", "Continue", new Vector2(200f, 56f), OnContinueClick);
            cont.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -120f);
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

            _completeChapters.Clear();
            ChapterDefinition[] chapters = ChapterCatalog.All;
            for (int i = 0; i < chapters.Length; i++)
            {
                if (ChapterCatalog.IsChapterComplete(chapters[i], _manager))
                    _completeChapters.Add(chapters[i].id);
            }
            _listDirty = true;
        }

        void OnObjectiveCompleted(ChallengeObjective obj, int index)
        {
            QhysicsSfx.Play2D(SfxId.ObjectiveDone, 0.6f);
            RefreshOverlay();
        }

        void OnChallengeCompleted(ChallengeDefinition def, int stars, int componentCount)
        {
            _listDirty = true;
            if (_overlayCanvas != null)
                _overlayCanvas.gameObject.SetActive(false);

            ChapterDefinition chapter = def != null ? ChapterCatalog.GetChapterFor(def.id) : null;
            bool chapterNewlyComplete = chapter != null && !_completeChapters.Contains(chapter.id)
                && ChapterCatalog.IsChapterComplete(chapter, _manager);
            if (chapterNewlyComplete)
                _completeChapters.Add(chapter.id);

            ShowBanner(def, stars, componentCount, chapterNewlyComplete ? chapter : null);
            QhysicsSfx.Play2D(chapterNewlyComplete ? SfxId.ChapterComplete : SfxId.ChallengeComplete, 0.8f);
        }

        void OnChallengeAbandoned()
        {
            if (_overlayCanvas != null)
                _overlayCanvas.gameObject.SetActive(false);
            _listDirty = true;
        }

        // ── Input ──────────────────────────────────────────────────

        void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                ToggleList();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            if (Input.GetKeyDown(KeyCode.C))
                ToggleList();
#endif
        }

        void HandleScrollInput()
        {
            if (_scroll == null || _listContent == null)
                return;
            float overflow = Mathf.Max(1f, _listContent.rect.height - ViewportHeight);
            float deltaPx = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                    deltaPx += Mathf.Sign(wheel) * Mathf.Min(Mathf.Abs(wheel), 240f) * 0.6f;
            }
#endif
            // Right (then left) thumbstick Y while the list is open.
            float stick = ReadStickY(XRNode.RightHand);
            if (Mathf.Abs(stick) < 0.2f)
                stick = ReadStickY(XRNode.LeftHand);
            if (Mathf.Abs(stick) >= 0.2f)
                deltaPx += stick * 900f * Time.unscaledDeltaTime;

            if (Mathf.Abs(deltaPx) > 0.001f)
            {
                // Positive = scroll up (toward the top of the list).
                _scroll.StopMovement();
                _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + deltaPx / overflow);
            }
        }

        static float ReadStickY(XRNode node)
        {
            XrInputDevice d = InputDevices.GetDeviceAtXRNode(node);
            if (!d.isValid)
                return 0f;
            if (d.TryGetFeatureValue(XrCommonUsages.primary2DAxis, out Vector2 v))
                return v.y;
            return 0f;
        }

        void Page(int dir)
        {
            if (_scroll == null || _listContent == null)
                return;
            float overflow = Mathf.Max(1f, _listContent.rect.height - ViewportHeight);
            float step = (ViewportHeight * 0.8f) / overflow;
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + dir * step);
            QhysicsSfx.Play2D(SfxId.UiTick, 0.4f);
        }

        public void ToggleList()
        {
            SetListVisible(!_listVisible);
        }

        public void SetListVisible(bool on)
        {
            if (!_built)
            {
                if (QhysicsUiBuilder.ResolveXrCamera() == null)
                    return;
                Build();
                _built = true;
            }
            _listVisible = on;
            QhysicsUiScrollGate.Set(GateOwner, on);
            if (_listCanvas != null)
                _listCanvas.gameObject.SetActive(on);
            if (on)
            {
                _snapList = true;
                if (_manager != null && _manager.IsChallengeActive)
                {
                    ChapterDefinition ch = ChapterCatalog.GetChapterFor(_manager.ActiveChallenge.id);
                    int idx = System.Array.IndexOf(ChapterCatalog.All, ch);
                    if (idx >= 0)
                        _chapterIndex = idx;
                }
                RefreshList();
                QhysicsSfx.Play2D(SfxId.UiTick, 0.4f);
            }
        }

        void SelectChapter(int idx)
        {
            _chapterIndex = Mathf.Clamp(idx, 0, ChapterCatalog.All.Length - 1);
            RefreshList();
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f;
            QhysicsSfx.Play2D(SfxId.UiTick, 0.4f);
        }

        // ── List refresh ───────────────────────────────────────────

        void RefreshList()
        {
            if (_listContent == null)
                return;
            TrySubscribeManager();
            if (_manager == null)
                return;
            _listDirty = false;

            float keep = _scroll != null ? _scroll.verticalNormalizedPosition : 1f;

            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                Transform c = _listContent.GetChild(i);
                c.SetParent(null, false);
                Destroy(c.gameObject);
            }

            ChapterDefinition[] chapters = ChapterCatalog.All;
            _chapterIndex = Mathf.Clamp(_chapterIndex, 0, chapters.Length - 1);
            ChapterDefinition chapter = chapters[_chapterIndex];

            for (int t = 0; t < _chapterTabs.Count; t++)
                QhysicsUiBuilder.SetChipSelected(_chapterTabs[t], t == _chapterIndex);

            if (_chapterSubtitle != null)
            {
                int stars = ChapterCatalog.CountStars(chapter, _manager);
                int max = (chapter.challengeIds != null ? chapter.challengeIds.Length : 0) * 3;
                _chapterSubtitle.text = chapter.subtitle + "   |   Stars " + stars + "/" + max;
            }

            if (chapter.challengeIds != null)
            {
                for (int i = 0; i < chapter.challengeIds.Length; i++)
                {
                    ChallengeDefinition def = _manager.FindChallenge(chapter.challengeIds[i]);
                    if (def != null)
                        BuildListEntry(def, i + 1);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
            if (_scroll != null)
            {
                _scroll.verticalNormalizedPosition = _snapList ? 1f : keep;
                _snapList = false;
            }
        }

        void BuildListEntry(ChallengeDefinition def, int number)
        {
            bool unlocked = _manager.IsUnlocked(def.id);
            int stars = _manager.GetStars(def.id);
            float bestTime = _manager.GetBestTime(def.id);
            int bestComp = _manager.GetBestComponentCount(def.id);
            bool active = _manager.IsChallengeActive && _manager.ActiveChallenge == def;
            float w = ListWidth - 50f;

            var entryRt = NewRect("Entry_" + def.id, _listContent, new Vector2(w, EntryHeight));

            Color bgColor = active ? QhysicsUiStyle.ChipBgActive : (unlocked ? QhysicsUiStyle.ChipBg : QhysicsUiStyle.TabIdle);
            var bg = QhysicsUiBuilder.Panel(entryRt, "Bg", bgColor, new Vector2(w, EntryHeight - 4f));
            bg.raycastTarget = true; // lets the XR ray / mouse drag the ScrollRect from anywhere on a row

            Color titleColor = unlocked ? QhysicsUiStyle.TextPrimary : QhysicsUiStyle.TextMuted;
            var title = QhysicsUiBuilder.Label(bg.transform, "Title", number + ". " + def.title,
                QhysicsUiStyle.FontBody, titleColor, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.anchoredPosition = new Vector2(-170f, 20f);
            title.rectTransform.sizeDelta = new Vector2(420f, 34f);

            string starText = FormatStars(stars);
            Color starColor = stars > 0 ? QhysicsUiStyle.AccentAttention : QhysicsUiStyle.TextMuted;
            var starsLabel = QhysicsUiBuilder.Label(bg.transform, "Stars", starText,
                QhysicsUiStyle.FontBody, starColor, TextAlignmentOptions.MidlineLeft);
            starsLabel.rectTransform.anchoredPosition = new Vector2(-280f, -18f);
            starsLabel.rectTransform.sizeDelta = new Vector2(200f, 30f);

            string stats = bestTime > 0f ? "Best: " + bestTime.ToString("0.0") + "s" : "";
            if (bestComp > 0)
                stats += " | " + bestComp + " parts";
            if (!unlocked)
                stats = "Finish the previous challenge to unlock";
            if (!string.IsNullOrEmpty(stats))
            {
                var statsLabel = QhysicsUiBuilder.Label(bg.transform, "Stats", stats,
                    QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.MidlineLeft);
                statsLabel.rectTransform.anchoredPosition = new Vector2(20f, -18f);
                statsLabel.rectTransform.sizeDelta = new Vector2(380f, 28f);
            }

            if (unlocked)
            {
                string btnLabel = active ? "Active" : (stars > 0 ? "Replay" : "Start");
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
            // ASCII stars (LiberationSans has no U+2605): lit '*' in gold, unlit in grey.
            int n = Mathf.Clamp(stars, 0, 3);
            return "<color=#F2D140>" + new string('*', n) + "</color><color=#5A636D>" + new string('*', 3 - n) + "</color>";
        }

        void OnStartClick(string challengeId)
        {
            if (_manager == null)
                return;
            if (_manager.IsChallengeActive && _manager.ActiveChallenge != null && _manager.ActiveChallenge.id == challengeId)
            {
                SetListVisible(false);
                return;
            }
            if (_manager.StartChallenge(challengeId))
            {
                QhysicsSfx.Play2D(SfxId.Unlock, 0.5f);
                SetListVisible(false);
                if (_overlayCanvas != null)
                    _overlayCanvas.gameObject.SetActive(true);
                RefreshOverlay();
            }
            else
            {
                QhysicsSfx.Play2D(SfxId.Denied, 0.5f);
            }
        }

        void OnAbandonClick()
        {
            if (_manager != null)
                _manager.AbandonChallenge();
        }

        void OnContinueClick()
        {
            if (_bannerCanvas != null)
                _bannerCanvas.gameObject.SetActive(false);
            SetListVisible(true);
        }

        // ── Overlay refresh ────────────────────────────────────────

        void RefreshOverlay()
        {
            if (_manager == null || _overlayCanvas == null)
                return;

            if (!_manager.IsChallengeActive)
            {
                if (_overlayCanvas.gameObject.activeSelf)
                    _overlayCanvas.gameObject.SetActive(false);
                return;
            }

            if (!_overlayCanvas.gameObject.activeSelf)
                _overlayCanvas.gameObject.SetActive(true);

            if (_overlayTitle != null && _manager.ActiveChallenge != null)
                _overlayTitle.text = _manager.ActiveChallenge.title;

            if (_overlayTimer != null)
                _overlayTimer.text = _manager.ElapsedTime.ToString("0.0") + "s";

            if (_overlayObjectives != null && _manager.ActiveObjectives != null)
            {
                var sb = new System.Text.StringBuilder(420);
                for (int i = 0; i < _manager.ActiveObjectives.Length; i++)
                {
                    ChallengeObjective obj = _manager.ActiveObjectives[i];
                    sb.Append(obj.completed ? "<color=#59E673>[x]</color> " : "[ ] ");
                    sb.Append(obj.displayText);
                    string live = _manager.GetObjectiveLiveReadout(obj);
                    if (!string.IsNullOrEmpty(live))
                    {
                        sb.Append("  [");
                        sb.Append(live);
                        sb.Append(']');
                    }
                    if (i < _manager.ActiveObjectives.Length - 1)
                        sb.Append("\n");
                }
                if (_manager.ActiveChallenge != null && !string.IsNullOrEmpty(_manager.ActiveChallenge.mentorHint))
                {
                    sb.Append("\n<color=#9AA3AD>");
                    sb.Append(_manager.ActiveChallenge.mentorHint);
                    sb.Append("</color>");
                }
                _overlayObjectives.text = sb.ToString();
            }
        }

        // ── Banner ─────────────────────────────────────────────────

        void ShowBanner(ChallengeDefinition def, int stars, int componentCount, ChapterDefinition completedChapter)
        {
            if (_bannerCanvas == null)
                return;
            _bannerCanvas.gameObject.SetActive(true);
            _bannerHideAt = Time.unscaledTime + BannerDuration + (completedChapter != null ? 3f : 0f);

            if (_bannerTitle != null)
            {
                if (completedChapter != null)
                    _bannerTitle.text = completedChapter.title.ToUpper() + " COMPLETE!";
                else
                    _bannerTitle.text = def != null ? def.title.ToUpper() + " COMPLETE!" : "COMPLETE!";
            }

            if (_bannerStars != null)
                _bannerStars.text = FormatStars(stars);

            if (_bannerStats != null)
            {
                string stats = "Time: " + (_manager != null ? _manager.ElapsedTime.ToString("0.0") : "0") + "s";
                if (componentCount > 0)
                    stats += "  |  Parts: " + componentCount;
                if (def != null && _manager != null)
                {
                    string[] next = _manager.GetNextUnlockTitles(def.id);
                    if (next != null && next.Length > 0)
                        stats += "\nUnlocked: " + string.Join(", ", next);
                }
                if (completedChapter != null)
                    stats += "\nEvery challenge in the chapter has a star. Sandbox / Extra stays open for harder combos.";
                _bannerStats.text = stats;
            }
        }

        // ── Positioning (each canvas independently) ────────────────

        void PositionListPanel()
        {
            if (!_listVisible || _listCanvas == null)
                return;
            Vector3 fwd = Flatten(_cam.transform.forward);
            Vector3 pos = _cam.transform.position + fwd * 1.25f + Vector3.up * 0.05f;
            Transform t = _listCanvas.transform;
            // Only drift when the player turns/moves away, so the ray can aim at a stable panel.
            if ((t.position - pos).sqrMagnitude > 0.35f * 0.35f || _snapList)
                t.position = Vector3.Lerp(t.position, pos, _snapList ? 1f : 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(t, _cam);
        }

        void PositionOverlay()
        {
            if (_overlayCanvas == null || !_overlayCanvas.gameObject.activeSelf)
                return;
            Vector3 fwd = Flatten(_cam.transform.forward);
            Vector3 right = Flatten(_cam.transform.right);
            Vector3 pos = _cam.transform.position + fwd * 0.75f + right * -0.38f + Vector3.up * 0.12f;
            Transform t = _overlayCanvas.transform;
            t.position = Vector3.Lerp(t.position, pos, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(t, _cam);
        }

        void PositionBanner()
        {
            if (_bannerCanvas == null || !_bannerCanvas.gameObject.activeSelf)
                return;
            Vector3 fwd = Flatten(_cam.transform.forward);
            Vector3 pos = _cam.transform.position + fwd * 1.0f + Vector3.up * 0.2f;
            Transform t = _bannerCanvas.transform;
            t.position = Vector3.Lerp(t.position, pos, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(t, _cam);
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }
}
