using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RealityEngine.Audio;
using RealityEngine.Challenges;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.UI
{
    /// <summary>
    /// Chapter-complete screen, shown ~2.5 s after the Chapter 1 finale (Transformer) is completed:
    /// chapter title, stars total (x / 30), per-level stars, Return to Menu / Keep Building.
    /// World-space panel fixed ~1.5 m ahead (re-centres only when you turn away), ray + trigger in XR,
    /// mouse on desktop (GraphicRaycaster + free cursor via QhysicsModal). Editor/dev: End previews it.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(208)]
    public sealed class QhysicsChapterCompleteScreen : MonoBehaviour
    {
        const float DelayAfterFinale = 2.5f;
        const float DistanceM = 1.45f;

        static QhysicsChapterCompleteScreen _instance;

        Canvas _canvas;
        TextMeshProUGUI _stars;
        TextMeshProUGUI _levels;
        Camera _cam;
        ChallengeManager _mgr;
        bool _open;
        bool _placed;
        float _showAt = -1f;

        public static bool IsOpen => _instance != null && _instance._open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var go = new GameObject("QhysicsChapterComplete");
            _instance = go.AddComponent<QhysicsChapterCompleteScreen>();
        }

        public static void ShowNow()
        {
            if (_instance == null)
                Boot();
            if (_instance != null)
                _instance.Open();
        }

        void Awake()
        {
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (_mgr != null)
                _mgr.OnChallengeCompleted -= OnCompleted;
            QhysicsModal.ChapterCompleteOpen = false;
        }

        void Build()
        {
            _canvas = QhysicsUiBuilder.CreateWorldCanvas("ChapterCompleteCanvas", transform, new Vector2(820f, 800f));
            _canvas.transform.localScale = Vector3.one * QhysicsUiStyle.CanvasScale * 1.25f;
            _canvas.sortingOrder = 125;
            Image face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(780f, 760f));
            QhysicsUiBuilder.LayoutVertical(face.rectTransform, 12f);
            var v = face.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(48, 48, 36, 32);
            v.childAlignment = TextAnchor.UpperCenter;

            var kicker = QhysicsUiBuilder.Label(face.transform, "Kicker", "CHAPTER 1 COMPLETE", QhysicsUiStyle.FontBody,
                QhysicsUiStyle.AccentActive, TextAlignmentOptions.Center);
            kicker.characterSpacing = 8f;
            kicker.rectTransform.sizeDelta = new Vector2(684f, 40f);
            var title = QhysicsUiBuilder.Label(face.transform, "Title", "Faraday's Bench", 56f,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.rectTransform.sizeDelta = new Vector2(684f, 70f);
            _stars = QhysicsUiBuilder.Label(face.transform, "Stars", "", 44f, QhysicsUiStyle.AccentAttention,
                TextAlignmentOptions.Center);
            _stars.rectTransform.sizeDelta = new Vector2(684f, 60f);
            _levels = QhysicsUiBuilder.Label(face.transform, "Levels", "", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            _levels.rectTransform.sizeDelta = new Vector2(684f, 300f);
            _levels.textWrappingMode = TextWrappingModes.NoWrap;
            var note = QhysicsUiBuilder.Label(face.transform, "Note",
                "Replay levels from the challenge list for 3 stars. Sandbox / Extra stays open.",
                QhysicsUiStyle.FontSmall - 2f, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            note.rectTransform.sizeDelta = new Vector2(684f, 34f);

            var row = new GameObject("Buttons", typeof(RectTransform));
            row.transform.SetParent(face.transform, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(684f, 100f);
            QhysicsUiBuilder.LayoutHorizontal(rowRt, 20f);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.padding = new RectOffset(0, 0, 4, 4);
            QhysicsUiBuilder.ChipButton(row.transform, "Menu", "Return to Menu", new Vector2(320f, 92f), ReturnToMenu);
            QhysicsUiBuilder.ChipButton(row.transform, "Keep", "Keep Building", new Vector2(320f, 92f), KeepBuilding);
            _canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_mgr == null && ChallengeManager.Instance != null)
            {
                _mgr = ChallengeManager.Instance;
                _mgr.OnChallengeCompleted += OnCompleted;
            }
            if (_showAt > 0f && Time.unscaledTime >= _showAt)
            {
                _showAt = -1f;
                Open();
            }
#if ENABLE_INPUT_SYSTEM && (UNITY_EDITOR || DEVELOPMENT_BUILD)
            var kb = Keyboard.current;
            if (kb != null && kb.endKey.wasPressedThisFrame && !QhysicsUiState.BootMenuOpen)
            {
                if (_open) Close();
                else Open();
            }
#endif
        }

        void OnCompleted(ChallengeDefinition def, int stars, int parts)
        {
            if (def != null && def.id == QhysicsChapterFlow.FinaleId)
                _showAt = Time.unscaledTime + DelayAfterFinale;
        }

        void Open()
        {
            if (_canvas == null)
                Build();
            Refresh();
            // Replace the short "chapter complete" banner so the two panels never overlap.
            GameObject cui = GameObject.Find("ChallengeUi");
            Transform banner = cui != null ? cui.transform.Find("BannerCanvas") : null;
            if (banner != null)
                banner.gameObject.SetActive(false);
            _open = true;
            _placed = false;
            QhysicsModal.ChapterCompleteOpen = true;
            _canvas.gameObject.SetActive(true);
            QhysicsSfx.Play2D(SfxId.ChapterComplete, 0.7f);
        }

        void Close()
        {
            _open = false;
            QhysicsModal.ChapterCompleteOpen = false;
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        void Refresh()
        {
            ChallengeManager mgr = ChallengeManager.Instance;
            int total = QhysicsChapterFlow.StarsTotal();
            _stars.text = "<color=#F2D140>*</color> " + total + " / " + QhysicsChapterFlow.StarsMax + " stars";
            var sb = new System.Text.StringBuilder(512);
            string[] ids = ChapterCatalog.Chapter1Ids;
            for (int i = 0; i < ids.Length; i++)
            {
                int s = mgr != null ? Mathf.Clamp(mgr.GetStars(ids[i]), 0, 3) : 0;
                sb.Append(i + 1).Append(". ").Append(QhysicsChapterFlow.LevelTitle(i)).Append("   ");
                sb.Append("<color=#F2D140>").Append(new string('*', s)).Append("</color>");
                sb.Append("<color=#5A636D>").Append(new string('*', 3 - s)).Append("</color>");
                if (i < ids.Length - 1)
                    sb.Append('\n');
            }
            _levels.text = sb.ToString();
        }

        void ReturnToMenu()
        {
            QhysicsSfx.Play2D(SfxId.UiTick, 0.5f);
            Close();
            QhysicsMainMenu menu = UnityEngine.Object.FindAnyObjectByType<QhysicsMainMenu>(FindObjectsInactive.Include);
            if (menu == null)
                menu = QhysicsMainMenu.Ensure(null);
            menu.SetVisible(true);
        }

        void KeepBuilding()
        {
            QhysicsSfx.Play2D(SfxId.UiTick, 0.5f);
            Close();
        }

        void LateUpdate()
        {
            if (!_open || _canvas == null)
                return;
            if (_cam == null || !_cam.isActiveAndEnabled)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;
            Vector3 fwd = _cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();
            Transform t = _canvas.transform;
            Vector3 to = t.position - _cam.transform.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (!_placed || Vector3.Angle(fwd, flat) > 55f || flat.sqrMagnitude > 3.5f * 3.5f || flat.sqrMagnitude < 0.3f)
            {
                Vector3 pos = _cam.transform.position + fwd * DistanceM + Vector3.up * 0.02f;
                t.position = _placed ? Vector3.Lerp(t.position, pos, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime)) : pos;
                _placed = true;
            }
            QhysicsUiBuilder.FaceCamera(t, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
            QhysicsModal.SyncDesktopRaycaster(_canvas);
        }
    }
}
