using UnityEngine;
using TMPro;

namespace RealityEngine.UI
{
    /// <summary>
    /// One-line dismissable lab strip: BOOT → ENTER → INTERACT → EXPERIMENT.
    /// Not a tip maze — progressive disclosure stays on Inspect / toolbelt.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(205)]
    public sealed class QhysicsOnboarding : MonoBehaviour
    {
        public const string RootName = "QhysicsOnboarding";
        const string PrefKey = "QHYSICS.Onboarding.Dismissed";

        const string StripText =
            "ENTER Sandbox  >  INTERACT (E / grip)  >  EXPERIMENT   |   M / Tab toolbelt   |   F1 controls";

        Canvas _canvas;
        TextMeshProUGUI _body;
        Camera _cam;
        bool _ready;

        public static QhysicsOnboarding Ensure(Transform parent)
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
                var c = existing.GetComponent<QhysicsOnboarding>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsOnboarding>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsOnboarding>();
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

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(980f, 96f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(940f, 72f));

            _body = QhysicsUiBuilder.Label(face.transform, "Strip", StripText, QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.MidlineLeft);
            _body.rectTransform.anchoredPosition = new Vector2(-40f, 0f);
            _body.rectTransform.sizeDelta = new Vector2(780f, 48f);

            QhysicsUiBuilder.ChipButton(face.transform, "Dismiss", "X", new Vector2(64f, 56f), Dismiss)
                .GetComponent<RectTransform>().anchoredPosition = new Vector2(420f, 0f);

            _ready = true;

            // 2026-09-27 declutter: the permanent breadcrumb strip is hidden by default; the same guidance now
            // appears once as the fading first-run hint card (QhysicsHintCard). ResetAndShow() still brings it back.
            SetVisible(false);
        }

        void LateUpdate()
        {
            if (!_ready || _canvas == null || !_canvas.gameObject.activeSelf)
                return;
            if (!QhysicsUiState.GameplayHudVisible)
            {
                SetVisible(false);
                return;
            }
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;

            Vector3 fwd = Flatten(_cam.transform.forward);
            Vector3 pos = _cam.transform.position + fwd * 0.95f + Vector3.up * (-0.32f);
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        public void Dismiss()
        {
            PlayerPrefs.SetInt(PrefKey, 1);
            PlayerPrefs.Save();
            SetVisible(false);
        }

        public void SetVisible(bool on)
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(on);
        }

        /// <summary>Editor / pause helper to show the strip again.</summary>
        public void ResetAndShow()
        {
            PlayerPrefs.SetInt(PrefKey, 0);
            if (_body != null)
                _body.text = StripText;
            SetVisible(true);
        }
    }
}
