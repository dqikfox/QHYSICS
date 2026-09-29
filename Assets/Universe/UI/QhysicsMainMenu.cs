using UnityEngine;
using TMPro;
using RealityEngine.XR;
using RealityEngine.Experiments;
using RealityEngine.Player;

namespace RealityEngine.UI
{
    /// <summary>
    /// Boot menu (every Play): Continue (last unlocked Chapter 1 level) / Chapter 1 "Faraday's Bench" / Sandbox / Settings.
    /// Clean centred world panel ~1.7 m ahead (fixed, not head-locked). While open the gameplay HUD is hidden (QhysicsUiState). Enter Sandbox loads into the usable Faraday lab.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(189)]
    public sealed class QhysicsMainMenu : MonoBehaviour
    {
        public const string RootName = "QhysicsMainMenu";
        const string PrefEntered = "QHYSICS.MainMenu.EnteredOnce";

        const float PreferredDistanceM = 1.7f;
        const float MinDistanceM = 0.9f;

        Canvas _canvas;
        Camera _cam;
        bool _visible;
        bool _placed;
        float _nextRefresh;
        UnityEngine.UI.Button _continueBtn;
        UnityEngine.UI.Button _chapterBtn;
        TextMeshProUGUI _continueLabel;
        TextMeshProUGUI _chapterLabel;

        public static QhysicsMainMenu Ensure(Transform parent)
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
                var c = existing.GetComponent<QhysicsMainMenu>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsMainMenu>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsMainMenu>();
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

            // Scaled up so it reads well at ~1.7 m (≈ same angular size as the old 1.2 m panel).
            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(720f, 640f));
            _canvas.transform.localScale = Vector3.one * QhysicsUiStyle.CanvasScale * 1.35f;
            _canvas.sortingOrder = 120;
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(680f, 600f));
            QhysicsUiBuilder.LayoutVertical(face.rectTransform, QhysicsUiStyle.Space2);
            var v = face.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            v.padding = new RectOffset(48, 48, 40, 32);
            v.childAlignment = TextAnchor.MiddleCenter;

            var title = QhysicsUiBuilder.Label(face.transform, "Title", "QHYSICS", 64f,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 12f;
            title.rectTransform.sizeDelta = new Vector2(584f, 76f);

            var sub = QhysicsUiBuilder.Label(face.transform, "Sub", "Faraday's bench  |  Giza plaza",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            sub.rectTransform.sizeDelta = new Vector2(584f, 40f);

            _continueBtn = QhysicsUiBuilder.ChipButton(face.transform, "Continue", "Continue", new Vector2(584f, 92f), ContinueGame);
            _continueLabel = _continueBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            _chapterBtn = QhysicsUiBuilder.ChipButton(face.transform, "Chapter1", "Chapter 1: Faraday's Bench", new Vector2(584f, 92f), PlayChapter1);
            _chapterLabel = _chapterBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            QhysicsUiBuilder.ChipButton(face.transform, "Enter", "Sandbox", new Vector2(584f, 80f), EnterSandbox);
            QhysicsUiBuilder.ChipButton(face.transform, "Settings", "Settings", new Vector2(584f, 64f), OpenSettings);

            var hint = QhysicsUiBuilder.Label(face.transform, "Hint", "F1 controls  |  Esc pause",
                QhysicsUiStyle.FontSmall - 2f, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            hint.rectTransform.sizeDelta = new Vector2(584f, 32f);
#if UNITY_EDITOR
            // no Exit here — pause panel has Exit Play
#endif

            // Boot menu on every Play: Continue / Chapter 1 / Sandbox.
            SetVisible(Application.isPlaying);
            RefreshButtons();
        }

        void LateUpdate()
        {
            if (!_visible || _canvas == null)
                return;
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.5f;
                RefreshButtons();
            }
            QhysicsModal.SyncDesktopRaycaster(_canvas);
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;
            Vector3 fwd = _cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();

            // Fixed in the world (not glued to the head): re-centre only on first show or when the player
            // turns away > 50 deg / moves > 1 m. Distance ~1.7 m, pulled in if scenery would intersect it.
            Vector3 toMenu = transform.position - _cam.transform.position;
            Vector3 flatTo = new Vector3(toMenu.x, 0f, toMenu.z);
            bool drifted = !_placed || flatTo.sqrMagnitude < 0.25f || flatTo.sqrMagnitude > 3.6f * 3.6f
                || Vector3.Angle(fwd, flatTo) > 50f;
            if (drifted)
            {
                float dist = PreferredDistanceM;
                if (UnityEngine.Physics.SphereCast(_cam.transform.position, 0.25f, fwd, out RaycastHit hit, PreferredDistanceM + 0.3f,
                        ~0, QueryTriggerInteraction.Ignore))
                    dist = Mathf.Clamp(hit.distance - 0.2f, MinDistanceM, PreferredDistanceM);
                Vector3 pos = _cam.transform.position + fwd * dist + Vector3.up * 0.02f;
                transform.position = _placed ? Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime)) : pos;
                if (!_placed || (transform.position - pos).sqrMagnitude < 0.0004f)
                    _placed = true;
            }
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
            QhysicsModal.SyncDesktopRaycaster(_canvas);
        }

        void EnterSandbox()
        {
            PlayerPrefs.SetInt(PrefEntered, 1);
            PlayerPrefs.Save();
            SetVisible(false);
            // Plaza spawn + desktop character stack (body, WASD, hotbar, BuildingBlock cam strip).
            QhysicsChapterFlow.EnterWorld();
            var mgr = RealityEngine.Challenges.ChallengeManager.Instance;
            if (mgr != null && mgr.IsChallengeActive)
                mgr.AbandonChallenge();
            QhysicsLevelIntroCard.Show("SANDBOX", "Free Build",
                QhysicsUiState.IsXr ? "Menu / B / Y opens the toolbelt. Build anything." : "M / Tab opens the toolbelt. Build anything.");
            QhysicsHintCard.ShowFirstRunOnce();
        }

        /// <summary>Continue: resume the last unlocked Chapter 1 level from saved progress.</summary>
        void ContinueGame()
        {
            int idx = QhysicsChapterFlow.ContinueIndex();
            if (idx < 0)
                return;
            StartLevelFromMenu(idx);
        }

        /// <summary>Chapter 1: fresh save starts level 1; with progress opens the chapter's level list.</summary>
        void PlayChapter1()
        {
            if (!QhysicsChapterFlow.HasProgress())
            {
                StartLevelFromMenu(0);
                return;
            }
            PlayerPrefs.SetInt(PrefEntered, 1);
            SetVisible(false);
            QhysicsChapterFlow.EnterWorld();
            RealityEngine.Challenges.ChallengeUi.OpenListStatic();
            QhysicsHintCard.ShowFirstRunOnce();
        }

        void StartLevelFromMenu(int idx)
        {
            PlayerPrefs.SetInt(PrefEntered, 1);
            PlayerPrefs.Save();
            SetVisible(false);
            QhysicsChapterFlow.EnterWorld();
            if (!QhysicsChapterFlow.StartLevel(idx))
                RealityEngine.Challenges.ChallengeUi.OpenListStatic();
            QhysicsHintCard.ShowFirstRunOnce();
        }

        void RefreshButtons()
        {
            if (_continueBtn == null)
                return;
            int idx = QhysicsChapterFlow.ContinueIndex();
            bool progress = QhysicsChapterFlow.HasProgress();
            _continueBtn.interactable = idx >= 0 && progress;
            if (_continueLabel != null)
            {
                _continueLabel.text = progress && idx >= 0
                    ? "Continue  |  Level " + (idx + 1) + ": " + QhysicsChapterFlow.LevelTitle(idx)
                    : "Continue  <color=#9AA3AD>(no saved progress)</color>";
            }
            if (_chapterLabel != null)
            {
                _chapterLabel.text = progress
                    ? "Chapter 1: Faraday's Bench  <color=#F2D140>" + QhysicsChapterFlow.StarsTotal() + "/" + QhysicsChapterFlow.StarsMax + "*</color>"
                    : "Chapter 1: Faraday's Bench  |  Start";
            }
        }

        void OpenSettings()
        {
            var settings = UnityEngine.Object.FindAnyObjectByType<QhysicsSettingsPanel>(FindObjectsInactive.Include);
            if (settings != null)
                settings.SetOpen(true);
        }

        public void SetVisible(bool on)
        {
            _visible = on;
            _placed = false;
            QhysicsUiState.BootMenuOpen = on;
            if (_canvas != null)
                _canvas.gameObject.SetActive(on);
        }
    }
}
