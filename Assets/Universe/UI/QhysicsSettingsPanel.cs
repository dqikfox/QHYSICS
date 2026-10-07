using UnityEngine;
using TMPro;
using RealityEngine.Audio;
using RealityEngine.XR;

namespace RealityEngine.UI
{
    /// <summary>
    /// World-space settings (two-column chip grid) with REAL wired toggles only (volume, sim speed, gravity, UI opacity, comfort vignette, teleport blink, snap turn angle, seated boost, foveation, walk speed, walk direction).
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

        // Two-column chip grid (12 setting chips => 6 rows); Close stays full width underneath.
        const int GridChips = 12;
        const int GridRows = (GridChips + 1) / 2;
        const float GridCellW = 340f, GridCellH = 64f, GridGapX = 16f, GridGapY = 10f;
        const float StatusH = 96f;

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
        TextMeshProUGUI _seatedLabel;
        TextMeshProUGUI _foveationLabel;
        TextMeshProUGUI _walkLabel;
        TextMeshProUGUI _walkDirLabel;

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

            // Two-column chip grid: the one-chip-per-row list had grown to 1.2 m tall at 1.1 m away, so the bottom
            // chips (and Close) sat far below eye level in the headset. The grid keeps every chip within an easy glance.
            float gridH = GridRows * GridCellH + (GridRows - 1) * GridGapY;
            float faceH = 16f + 48f + 10f + StatusH + 10f + gridH + 10f + 72f + 16f;
            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(780f, faceH + 40f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(740f, faceH));
            QhysicsUiBuilder.LayoutVertical(face.rectTransform, 10f);

            QhysicsUiBuilder.Label(face.transform, "Title", "SETTINGS", QhysicsUiStyle.FontTitle,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.Center).rectTransform.sizeDelta = new Vector2(700f, 48f);

            _status = QhysicsUiBuilder.Label(face.transform, "Status", "", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            _status.rectTransform.sizeDelta = new Vector2(700f, StatusH);
            _status.textWrappingMode = TextWrappingModes.Normal; // the status line wraps over 2-3 lines instead of ellipsing
            _status.overflowMode = TextOverflowModes.Truncate;

            var gridRt = new GameObject("Grid", typeof(RectTransform)).GetComponent<RectTransform>();
            gridRt.SetParent(face.transform, false);
            gridRt.sizeDelta = new Vector2(700f, gridH);
            var grid = gridRt.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(GridCellW, GridCellH);
            grid.spacing = new Vector2(GridGapX, GridGapY);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            Transform g = gridRt;

            GridChip(g, "VolDown", "Volume -", () => NudgeVolume(-0.1f));
            GridChip(g, "VolUp", "Volume +", () => NudgeVolume(0.1f));
            GridChip(g, "Speed", "Cycle sim speed", CycleSpeed);
            GridChip(g, "Gravity", "Cycle gravity", CycleGravity);
            GridChip(g, "Opacity", "UI opacity cycle", CycleOpacity);
            _vignetteLabel = GridChip(g, "Vignette", VignetteText(), CycleVignette);
            _blinkLabel = GridChip(g, "Blink", BlinkText(), ToggleBlink);
            _snapLabel = GridChip(g, "SnapTurn", SnapText(), CycleSnapTurn);
            _seatedLabel = GridChip(g, "Seated", SeatedText(), CycleSeated);
            _foveationLabel = GridChip(g, "Foveation", FoveationText(), CycleFoveation);
            _walkLabel = GridChip(g, "WalkSpeed", WalkText(), CycleWalkSpeed);
            _walkDirLabel = GridChip(g, "WalkDirection", WalkDirText(), ToggleWalkDirection);
            QhysicsUiBuilder.ChipButton(face.transform, "Close", "Close", new Vector2(520f, 72f), () => SetOpen(false));

            // Boot path: apply PlayerPrefs master to mixer MasterVolume (dB). AudioListener stays at 1 when mixer is present.
            _masterVolume = AudioRouter.LoadMasterVolume();
            AudioRouter.SetMasterVolume(_masterVolume);
            MatchGravityIndex();
            RefreshStatus();
            SetOpen(false);
        }

        /// <summary>One half-width settings chip in the grid; returns its label so cycling chips can rename themselves.</summary>
        static TextMeshProUGUI GridChip(Transform grid, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            var chip = QhysicsUiBuilder.ChipButton(grid, name, label, new Vector2(GridCellW, GridCellH), onClick);
            var tmp = chip.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                // Long values ("Walk direction: Left hand") shrink a little instead of being cut off with "...".
                tmp.enableAutoSizing = true;
                tmp.fontSizeMax = QhysicsUiStyle.FontChip;
                tmp.fontSizeMin = 17f;
            }
            return tmp;
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

        static string SeatedText() => "Seated boost: " + SeatedMode.Label;

        void CycleSeated()
        {
            // Headset comfort: play sitting down - raises your view and hands by 30 or 50 cm (Off / +30 cm / +50 cm).
            SeatedMode.Cycle();
            if (_seatedLabel != null)
                _seatedLabel.text = SeatedText();
            RefreshStatus();
        }

        static string FoveationText() => "Foveation: " + FoveationSettings.Label;

        void CycleFoveation()
        {
            // Quest performance: renders the soft lens edges at lower resolution for a steadier frame rate (Off / Low / Medium / High).
            FoveationSettings.Cycle();
            if (_foveationLabel != null)
                _foveationLabel.text = FoveationText();
            RefreshStatus();
        }

        static string WalkText() => "Walk speed: " + WalkSpeedSettings.Label;

        void CycleWalkSpeed()
        {
            // Headset comfort: how fast the left stick walks you (Slow 1.2 / Normal 2.0 / Fast 3.0 m/s). Applies immediately.
            WalkSpeedSettings.Cycle();
            if (_walkLabel != null)
                _walkLabel.text = WalkText();
            RefreshStatus();
        }

        static string WalkDirText() => "Walk direction: " + WalkDirectionSettings.Label;

        void ToggleWalkDirection()
        {
            // Headset comfort: left stick walks where you look (Head) or where the left controller points (Left hand).
            WalkDirectionSettings.Cycle();
            if (_walkDirLabel != null)
                _walkDirLabel.text = WalkDirText();
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
                + " | snap " + SnapTurnSettings.Label
                + " | seated " + SeatedMode.Label
                + " | foveation " + FoveationSettings.Label
                + " | walk " + WalkSpeedSettings.Label
                + " | walk dir " + WalkDirectionSettings.Label;
        }
    }
}
