using UnityEngine;
using TMPro;

namespace RealityEngine.UI
{
    /// <summary>
    /// World-space settings with REAL wired toggles only (volume, sim speed, gravity, UI opacity).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(206)]
    public sealed class QhysicsSettingsPanel : MonoBehaviour
    {
        public const string RootName = "QhysicsSettingsPanel";

        static readonly float[] GravityPresets = { 9.81f, 1.62f, 3.71f }; // Earth, Moon, Mars
        static readonly string[] GravityNames = { "Earth g", "Moon g", "Mars g" };
        static readonly float[] Speeds = { 0.5f, 1f, 2f };

        Canvas _canvas;
        TextMeshProUGUI _status;
        Camera _cam;
        bool _open;
        int _gravIndex;
        float _uiOpacity = 0.82f;

        public static QhysicsSettingsPanel Ensure(Transform parent)
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
                var c = existing.GetComponent<QhysicsSettingsPanel>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsSettingsPanel>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsSettingsPanel>();
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

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(780f, 620f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(740f, 580f));
            QhysicsUiBuilder.LayoutVertical(face.rectTransform, 10f);

            QhysicsUiBuilder.Label(face.transform, "Title", "SETTINGS", QhysicsUiStyle.FontTitle,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.Center).rectTransform.sizeDelta = new Vector2(700f, 48f);

            _status = QhysicsUiBuilder.Label(face.transform, "Status", "", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            _status.rectTransform.sizeDelta = new Vector2(700f, 36f);

            QhysicsUiBuilder.ChipButton(face.transform, "VolDown", "Volume -", new Vector2(520f, 72f), () => NudgeVolume(-0.1f));
            QhysicsUiBuilder.ChipButton(face.transform, "VolUp", "Volume +", new Vector2(520f, 72f), () => NudgeVolume(0.1f));
            QhysicsUiBuilder.ChipButton(face.transform, "Speed", "Cycle sim speed", new Vector2(520f, 72f), CycleSpeed);
            QhysicsUiBuilder.ChipButton(face.transform, "Gravity", "Cycle gravity", new Vector2(520f, 72f), CycleGravity);
            QhysicsUiBuilder.ChipButton(face.transform, "Opacity", "UI opacity cycle", new Vector2(520f, 72f), CycleOpacity);
            QhysicsUiBuilder.ChipButton(face.transform, "Close", "Close", new Vector2(520f, 72f), () => SetOpen(false));

            MatchGravityIndex();
            RefreshStatus();
            SetOpen(false);
        }

        void LateUpdate()
        {
            if (!_open || _canvas == null)
                return;
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;
            Vector3 fwd = _cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
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
                RefreshStatus();
        }

        void NudgeVolume(float delta)
        {
            AudioListener.volume = Mathf.Clamp01(AudioListener.volume + delta);
            RefreshStatus();
        }

        void CycleSpeed()
        {
            float cur = Time.timeScale < 0.01f ? 1f : Time.timeScale;
            int idx = 0;
            float best = float.MaxValue;
            for (int i = 0; i < Speeds.Length; i++)
            {
                float d = Mathf.Abs(Speeds[i] - cur);
                if (d < best) { best = d; idx = i; }
            }
            idx = (idx + 1) % Speeds.Length;
            Time.timeScale = Speeds[idx];
            RefreshStatus();
        }

        void CycleGravity()
        {
            _gravIndex = (_gravIndex + 1) % GravityPresets.Length;
            float g = GravityPresets[_gravIndex];
            UnityEngine.Physics.gravity = new Vector3(0f, -g, 0f);
            RefreshStatus();
        }

        void CycleOpacity()
        {
            _uiOpacity += 0.15f;
            if (_uiOpacity > 1.01f)
                _uiOpacity = 0.4f;
            ApplyOpacity(_uiOpacity);
            RefreshStatus();
        }

        void ApplyOpacity(float a)
        {
            var imgs = GetComponentsInParent<UnityEngine.UI.Image>(true);
            // Prefer root QhysicsUI canvases
            Transform root = transform.parent != null ? transform.parent : transform;
            imgs = root.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            for (int i = 0; i < imgs.Length; i++)
            {
                if (imgs[i] == null) continue;
                Color c = imgs[i].color;
                // Only nudge panel-like dark backgrounds
                if (c.a < 0.2f) continue;
                if (c.r > 0.5f && c.g > 0.5f && c.b > 0.5f) continue;
                c.a = Mathf.Clamp01(a);
                imgs[i].color = c;
            }
        }

        void MatchGravityIndex()
        {
            float g = Mathf.Abs(UnityEngine.Physics.gravity.y);
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < GravityPresets.Length; i++)
            {
                float d = Mathf.Abs(GravityPresets[i] - g);
                if (d < bestD) { bestD = d; best = i; }
            }
            _gravIndex = best;
        }

        void RefreshStatus()
        {
            if (_status == null) return;
            _status.text = "vol " + AudioListener.volume.ToString("0.00")
                + " | speed " + Time.timeScale.ToString("0.##") + "x"
                + " | " + GravityNames[_gravIndex]
                + " | ui α " + _uiOpacity.ToString("0.00");
        }
    }
}