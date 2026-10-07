using UnityEngine;
using TMPro;
using RealityEngine.Audio;
using RealityEngine.XR;

namespace RealityEngine.UI
{
    /// <summary>
    /// World-space settings with REAL wired toggles only (volume, sim speed, gravity, UI opacity, comfort vignette, teleport blink, snap turn angle).
    /// Volume chips drive QhysicsMixer Master (dB) via <see cref="AudioRouter"/> — not AudioListener.volume.
    /// Honesty: Master bus gain only; not per-SFX routing and not a full EQ.
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
        float _masterVolume = 1f;
        TextMeshProUGUI _vignetteLabel;
        TextMeshProUGUI _blinkLabel;
        TextMeshProUGUI _snapLabel;

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

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(780f, 872f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(740f, 832f));
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
            var vig = QhysicsUiBuilder.ChipButton(face.transform, "Vignette", VignetteText(), new Vector2(520f, 72f), CycleVignette);
            _vignetteLabel = vig.GetComponentInChildren<TextMeshProUGUI>(true);
            var blink = QhysicsUiBuilder.ChipButton(face.transform, "Blink", BlinkText(), new Vector2(520f, 72f), ToggleBlink);
            _blinkLabel = blink.GetComponentInChildren<TextMeshProUGUI>(true);
            var snap = QhysicsUiBuilder.ChipButton(face.transform, "SnapTurn", SnapText(), new Vector2(520f, 72f), CycleSnapTurn);
            _snapLabel = snap.GetComponentInChildren<TextMeshProUGUI>(true);
            QhysicsUiBuilder.ChipButton(face.transform, "Close", "Close", new Vector2(520f, 72f), () => SetOpen(false));

            // Boot path: apply PlayerPrefs master to mixer MasterVolume (dB). AudioListener stays at 1 when mixer is present.
            _masterVolume = AudioRouter.LoadMasterVolume();
            AudioRouter.SetMasterVolume(_masterVolume);
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
            _masterVolume = Mathf.Clamp01(_masterVolume + delta);
            // Persist + apply QhysicsMixer MasterVolume (linear 0-1 -> dB). Does not set AudioListener.volume when mixer is loaded.
            AudioRouter.SaveMasterVolume(_masterVolume);
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

        static string VignetteText() => "Comfort vignette: " + ComfortVignette.ModeLabel(ComfortVignette.CurrentMode);

        void CycleVignette()
        {
            // Headset comfort: darkens the view edges while you stick-walk / smooth-turn (Mild / Strong / Off).
            ComfortVignette.CycleMode();
            if (_vignetteLabel != null)
                _vignetteLabel.text = VignetteText();
            RefreshStatus();
        }

        static string BlinkText() => "Teleport blink: " + TeleportBlink.StateLabel;

        void ToggleBlink()
        {
            // Headset comfort: a quick fade to black and back whenever you teleport or travel (On / Off).
            TeleportBlink.Toggle();
            if (_blinkLabel != null)
                _blinkLabel.text = BlinkText();
            RefreshStatus();
        }

        static string SnapText() => "Snap turn: " + SnapTurnSettings.Label;

        void CycleSnapTurn()
        {
            // Headset comfort: how far one right-stick flick turns you (30 / 45 / 90 degrees). Applies immediately.
            SnapTurnSettings.Cycle();
            if (_snapLabel != null)
                _snapLabel.text = SnapText();
            RefreshStatus();
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
            float db = AudioRouter.ToDb(_masterVolume);
            string volBit = AudioRouter.HasMixer
                ? "vol " + _masterVolume.ToString("0.00") + " (" + db.ToString("0.0") + " dB Master)"
                : "vol " + _masterVolume.ToString("0.00") + " (listener fallback)";
            _status.text = volBit
                + " | speed " + Time.timeScale.ToString("0.##") + "x"
                + " | " + GravityNames[_gravIndex]
                + " | ui a " + _uiOpacity.ToString("0.00")
                + " | vignette " + ComfortVignette.ModeLabel(ComfortVignette.CurrentMode)
                + " | blink " + TeleportBlink.StateLabel
                + " | snap " + SnapTurnSettings.Label;
        }
    }
}
