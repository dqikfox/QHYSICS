using UnityEngine;
using TMPro;

namespace RealityEngine.UI
{
    /// <summary>
    /// Simple world-space main entry: Enter Sandbox loads into usable Faraday lab.
    /// Skipped if PlayerPrefs marks sandbox already entered this install (optional).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(189)]
    public sealed class QhysicsMainMenu : MonoBehaviour
    {
        public const string RootName = "QhysicsMainMenu";
        const string PrefEntered = "QHYSICS.MainMenu.EnteredOnce";

        Canvas _canvas;
        Camera _cam;
        bool _visible;

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

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(820f, 480f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(780f, 440f));
            QhysicsUiBuilder.LayoutVertical(face.rectTransform, 14f);

            QhysicsUiBuilder.Label(face.transform, "Title", "QHYSICS", QhysicsUiStyle.FontTitle,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.Center).rectTransform.sizeDelta = new Vector2(740f, 56f);

            var sub = QhysicsUiBuilder.Label(face.transform, "Sub", "Faraday circuit sandbox + Giza plaza",
                QhysicsUiStyle.FontBody, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            sub.rectTransform.sizeDelta = new Vector2(740f, 40f);

            QhysicsUiBuilder.ChipButton(face.transform, "Enter", "Enter Sandbox", new Vector2(560f, 96f), EnterSandbox);
            QhysicsUiBuilder.ChipButton(face.transform, "Settings", "Settings", new Vector2(560f, 80f), OpenSettings);
#if UNITY_EDITOR
            // no Exit here — pause panel has Exit Play
#endif

            // Show on first Play; if already entered once, stay hidden (lab is ready immediately).
            bool show = Application.isPlaying && PlayerPrefs.GetInt(PrefEntered, 0) == 0;
            SetVisible(show);
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
            Vector3 pos = _cam.transform.position + fwd * 1.2f + Vector3.up * 0.08f;
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
        }

        void EnterSandbox()
        {
            PlayerPrefs.SetInt(PrefEntered, 1);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            SetVisible(false);
            // Ensure player parked at lab
            var spawn = UnityEngine.Object.FindAnyObjectByType<RealityEngine.XR.LabPlayerSpawn>(FindObjectsInactive.Include);
            if (spawn != null)
                spawn.ApplyNow(true);
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
            if (_canvas != null)
                _canvas.gameObject.SetActive(on);
        }
    }
}