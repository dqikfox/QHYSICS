using UnityEngine;
using TMPro;
using RealityEngine.XR;
using RealityEngine.Experiments;
using RealityEngine.Player;

namespace RealityEngine.UI
{
    /// <summary>
    /// Boot menu: clean centred world panel ~1.7 m ahead (fixed, not head-locked). While open the gameplay HUD is hidden (QhysicsUiState). Enter Sandbox loads into the usable Faraday lab.
    /// Skipped if PlayerPrefs marks sandbox already entered this install (optional).
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
            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(720f, 520f));
            _canvas.transform.localScale = Vector3.one * QhysicsUiStyle.CanvasScale * 1.35f;
            _canvas.sortingOrder = 120;
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(680f, 480f));
            QhysicsUiBuilder.LayoutVertical(face.rectTransform, QhysicsUiStyle.Space2);
            var v = face.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            v.padding = new RectOffset(48, 48, 40, 32);
            v.childAlignment = TextAnchor.MiddleCenter;

            var title = QhysicsUiBuilder.Label(face.transform, "Title", "QHYSICS", 64f,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 12f;
            title.rectTransform.sizeDelta = new Vector2(584f, 76f);

            var sub = QhysicsUiBuilder.Label(face.transform, "Sub", "Faraday circuit sandbox  |  Giza plaza",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            sub.rectTransform.sizeDelta = new Vector2(584f, 40f);

            QhysicsUiBuilder.ChipButton(face.transform, "Enter", "Enter Sandbox", new Vector2(584f, 96f), EnterSandbox);
            QhysicsUiBuilder.ChipButton(face.transform, "Settings", "Settings", new Vector2(584f, 80f), OpenSettings);

            var hint = QhysicsUiBuilder.Label(face.transform, "Hint", "F1 controls  |  Esc pause",
                QhysicsUiStyle.FontSmall - 2f, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            hint.rectTransform.sizeDelta = new Vector2(584f, 32f);
#if UNITY_EDITOR
            // no Exit here — pause panel has Exit Play
#endif

            // Show on first Play; if already entered once, stay hidden (lab is ready immediately).
            bool show = Application.isPlaying && PlayerPrefs.GetInt(PrefEntered, 0) == 0;
            SetVisible(show);
            if (!show && Application.isPlaying)
                QhysicsHintCard.ShowFirstRunOnce();
        }

        void LateUpdate()
        {
            if (!_visible || _canvas == null)
                return;
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
        }

        void EnterSandbox()
        {
            PlayerPrefs.SetInt(PrefEntered, 1);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            SetVisible(false);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            // Plaza spawn + desktop character stack (body, WASD, hotbar, BuildingBlock cam strip).
            LabPlayerSpawn.EnsureApplied();
            QhysicsDesktopBootstrap.Ensure();
            LabPlayerSpawn.RecalibratePlayerHeight(force: true);
            QhysicsHintCard.ShowFirstRunOnce();
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
